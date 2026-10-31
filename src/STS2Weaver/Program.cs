// STS2Weaver statically weaves Harmony-style prefix/postfix hooks into sts2.dll; iOS NativeAOT has no JIT for runtime patching.
// Usage: STS2Weaver <sts2.dll> <hooks.dll> <manifest.json> <output.dll>
//
// manifest.json: { "patches": [ { "targetType", "targetMethod", "kind": "prefix"|"postfix",
//                                 "hookType", "hookMethod" } ] }
//
// Semantics follow the subset of HarmonyLib used by this project:
// A void prefix is called at the start of the original method.
// A bool prefix runs at entry; false skips the original body and returns immediately.
// For value-returning methods, return the __result local, or default if no __result is bound.
// A postfix runs at each return via a shared exit epilogue.
// Bind __instance to this and __result/ref __result to the return-value local.
// Match other arguments to original parameter names, including ref parameters.
// Unsupported: transpilers, finalizers, __state, ___field injection and generic targets. Ambiguous overloads require explicit parameters.

using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: STS2Weaver <sts2.dll> <hooks.dll> <manifest.json> <output.dll>");
    return 2;
}

string targetPath = args[0], hooksPath = args[1], manifestPath = args[2], outputPath = args[3];

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath)));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(hooksPath)));

var readParams = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false };
var targetAsm = AssemblyDefinition.ReadAssembly(targetPath, readParams);
var hooksAsm = AssemblyDefinition.ReadAssembly(hooksPath, readParams);
var module = targetAsm.MainModule;

var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("Failed to parse manifest");

// stripReadonly removes initonly from selected static fields, allowing reflection SetValue on .NET 8.
foreach (var s in manifest.StripReadonly ?? new())
{
    var t = module.GetType(s.Type) ?? throw new InvalidOperationException($"stripReadonly type not found: {s.Type}");
    foreach (var fname in s.Fields)
    {
        var f = t.Fields.FirstOrDefault(x => x.Name == fname)
            ?? throw new InvalidOperationException($"stripReadonly field not found: {s.Type}.{fname}");
        f.IsInitOnly = false;
        Console.WriteLine($"[OK] strip-ro {s.Type}.{fname}");
    }
}

// patchConst replaces integer literals in the selected type and nested state machines; useful for inlined constants such as maxFloor=49.
foreach (var pc in manifest.PatchConsts ?? new())
{
    var t = module.GetType(pc.Type) ?? throw new InvalidOperationException($"patchConst type not found: {pc.Type}");
    int count = 0;
    var allTypes = new List<TypeDefinition> { t };
    allTypes.AddRange(t.NestedTypes);
    foreach (var td in allTypes)
        foreach (var m in td.Methods.Where(x => x.HasBody))
        {
            if (pc.MethodContains != null && !td.Name.Contains(pc.MethodContains) && !m.Name.Contains(pc.MethodContains)) continue;
            foreach (var ins in m.Body.Instructions)
            {
                bool hit = (ins.OpCode == OpCodes.Ldc_I4_S && (sbyte)ins.Operand == pc.FromInt)
                        || (ins.OpCode == OpCodes.Ldc_I4 && (int)ins.Operand == pc.FromInt);
                if (!hit) continue;
                ins.OpCode = OpCodes.Ldc_I4; ins.Operand = pc.ToInt; count++;
                Console.WriteLine($"[OK] patch-const {td.Name}.{m.Name}: {pc.FromInt}→{pc.ToInt}");
            }
        }
    if (count == 0) throw new InvalidOperationException($"patchConst found no occurrences of {pc.FromInt}: {pc.Type} ({pc.MethodContains})");
}

// redirectCalls replaces matching calls in a namespace with a same-signature hook, such as a 1ms Task.Delay variant.
foreach (var rc in manifest.RedirectCalls ?? new())
{
    var hookTypeDef = hooksAsm.MainModule.GetType(rc.HookType)
        ?? throw new InvalidOperationException($"Redirect hook type not found: {rc.HookType}");
    var hook = hookTypeDef.Methods.SingleOrDefault(m => m.Name == rc.HookMethod && m.IsStatic && m.IsPublic)
        ?? throw new InvalidOperationException($"Redirect hook method not found: {rc.HookMethod}");
    var hookRef = module.ImportReference(hook);
    int n = 0;
    foreach (var td in module.GetTypes())   // Include nested types such as async state machines.
    {
        if (!td.FullName.StartsWith(rc.NamespacePrefix)) continue;
        foreach (var m in td.Methods.Where(x => x.HasBody))
            foreach (var ins in m.Body.Instructions)
            {
                if (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt) continue;
                if (ins.Operand is not MethodReference mr) continue;
                bool match;
                if (rc.GenericArg != null)
                {
                    // Match generic instance calls, such as Rng.NextItem<NEventOptionButton>, by method and generic argument.
                    // For instance-to-static redirection, the receiver becomes the first argument; callers must ensure compatible signatures.
                    match = mr is GenericInstanceMethod gim
                        && gim.ElementMethod.Name == rc.TargetMethod
                        && gim.ElementMethod.DeclaringType.FullName == rc.TargetType
                        && gim.GenericArguments.Count == 1
                        && gim.GenericArguments[0].FullName == rc.GenericArg;
                }
                else
                {
                    // Compare parameter types to avoid changing unrelated overloads, such as Delay(TimeSpan,ct) versus Delay(int,ct).
                    match = mr.DeclaringType.FullName == rc.TargetType && mr.Name == rc.TargetMethod
                        && mr.Parameters.Count == hook.Parameters.Count
                        && mr.Parameters.Select((pp, i) => pp.ParameterType.FullName == hook.Parameters[i].ParameterType.FullName).All(x => x);
                }
                if (!match) continue;
                ins.Operand = hookRef;
                ins.OpCode = OpCodes.Call;   // A static hook requires call; callvirt on a static method is invalid IL.
                n++;
            }
    }
    Console.WriteLine($"[OK] redirect {rc.TargetType}.{rc.TargetMethod}({string.Join(",", hook.Parameters.Select(x => x.ParameterType.Name))}) → {rc.HookMethod} x{n} (scope: {rc.NamespacePrefix})");
    if (n == 0) throw new InvalidOperationException($"redirectCalls matched no calls: {rc.TargetType}.{rc.TargetMethod}");
}

int woven = 0, failed = 0;
foreach (var p in manifest.Patches)
{
    try
    {
        WeaveOne(p);
        woven++;
        Console.WriteLine($"[OK] {p.Kind,-7} {p.TargetType}.{p.TargetMethod} <= {p.HookType}.{p.HookMethod}");
    }
    catch (Exception e)
    {
        failed++;
        Console.Error.WriteLine($"[FAIL] {p.Kind} {p.TargetType}.{p.TargetMethod}: {e.Message}");
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"Weaving failed at {failed} hooks; refusing to write partial output");
    return 1;
}

targetAsm.Write(outputPath);
Console.WriteLine($"Complete: {woven} hooks woven → {outputPath}");
return 0;

void WeaveOne(PatchEntry p)
{
    var targetType = module.GetType(p.TargetType)
        ?? throw new InvalidOperationException($"Target type not found: {p.TargetType}");
    var candidates = targetType.Methods.Where(m => m.Name == p.TargetMethod).ToList();
    if (candidates.Count == 0) throw new InvalidOperationException($"Target method not found: {p.TargetMethod}");
    if (candidates.Count > 1 && p.TargetParams != null)
    {
        // Disambiguate overloads by simple or fully qualified parameter type names.
        candidates = candidates.Where(m =>
            m.Parameters.Count == p.TargetParams.Count &&
            m.Parameters.Select((pr, i) => pr.ParameterType.Name == p.TargetParams[i] || pr.ParameterType.FullName == p.TargetParams[i]).All(x => x)
        ).ToList();
    }
    if (candidates.Count > 1) throw new InvalidOperationException($"Target has {candidates.Count} overloads; specify targetParams: {p.TargetMethod}");
    var target = candidates[0];
    if (target.HasGenericParameters) throw new InvalidOperationException("Generic target methods are unsupported");
    if (!target.HasBody) throw new InvalidOperationException("Target method has no body");

    var hookTypeDef = hooksAsm.MainModule.GetType(p.HookType)
        ?? throw new InvalidOperationException($"Hook type not found: {p.HookType}");
    var hook = hookTypeDef.Methods.SingleOrDefault(m => m.Name == p.HookMethod && m.IsStatic && m.IsPublic)
        ?? throw new InvalidOperationException($"Hook method missing or not public static: {p.HookMethod}");

    bool isPrefix = p.Kind.Equals("prefix", StringComparison.OrdinalIgnoreCase);
    bool isPostfix = p.Kind.Equals("postfix", StringComparison.OrdinalIgnoreCase);
    if (!isPrefix && !isPostfix) throw new InvalidOperationException($"Unknown kind: {p.Kind}");

    var hookReturnsBool = hook.ReturnType.MetadataType == MetadataType.Boolean;
    var hookReturnsVoid = hook.ReturnType.MetadataType == MetadataType.Void;
    if (isPrefix && !hookReturnsBool && !hookReturnsVoid)
        throw new InvalidOperationException("Prefix hook must return bool or void");
    if (isPostfix && !hookReturnsVoid)
        throw new InvalidOperationException("Postfix hook must return void");

    var body = target.Body;
    body.InitLocals = true;
    body.SimplifyMacros();
    var il = body.GetILProcessor();
    var returnsValue = target.ReturnType.MetadataType != MetadataType.Void;

    // Create the __result local on demand.
    VariableDefinition resultVar = null;
    VariableDefinition EnsureResultVar()
    {
        if (!returnsValue) throw new InvalidOperationException("Cannot bind __result on a void target");
        resultVar ??= AddLocal(body, target.ReturnType);
        return resultVar;
    }

    // Generate instructions to load hook arguments.
    List<Instruction> BuildArgLoads(bool inPostfixEpilogue)
    {
        var loads = new List<Instruction>();
        foreach (var hp in hook.Parameters)
        {
            var name = hp.Name;
            if (name == "__instance")
            {
                if (target.IsStatic) throw new InvalidOperationException("A static target has no __instance");
                loads.Add(il.Create(OpCodes.Ldarg_0));
            }
            else if (name == "__result")
            {
                var v = EnsureResultVar();
                if (hp.ParameterType.IsByReference) loads.Add(il.Create(OpCodes.Ldloca, v));
                else
                {
                    if (!inPostfixEpilogue) throw new InvalidOperationException("A prefix __result parameter must be ref");
                    loads.Add(il.Create(OpCodes.Ldloc, v));
                }
            }
            else
            {
                var op = target.Parameters.FirstOrDefault(x => x.Name == name)
                    ?? throw new InvalidOperationException($"Hook parameter '{name}' has no matching target parameter");
                loads.Add(hp.ParameterType.IsByReference && !op.ParameterType.IsByReference
                    ? il.Create(OpCodes.Ldarga, op)
                    : il.Create(OpCodes.Ldarg, op));
            }
        }
        return loads;
    }

    var hookRef = module.ImportReference(hook);

    if (isPrefix)
    {
        var first = body.Instructions[0];
        var seq = BuildArgLoads(inPostfixEpilogue: false);
        seq.Add(il.Create(OpCodes.Call, hookRef));
        if (hookReturnsBool)
        {
            seq.Add(il.Create(OpCodes.Brtrue, first)); // true: continue into the original method.
            if (returnsValue) seq.Add(il.Create(OpCodes.Ldloc, resultVar ?? EnsureResultVar()));
            seq.Add(il.Create(OpCodes.Ret));
        }
        foreach (var ins in seq) il.InsertBefore(first, ins);
        // Existing branches targeting first bypass the prefix correctly: the prefix runs only once at entry.
    }
    else // Postfix: rewrite to a shared exit.
    {
        var rets = body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();
        if (body.HasExceptionHandlers)
        {
            foreach (var h in body.ExceptionHandlers)
                foreach (var r in rets)
                    if (InRange(body, r, h.TryStart, h.TryEnd) || InRange(body, r, h.HandlerStart, h.HandlerEnd))
                        throw new InvalidOperationException("Postfix target returns inside try/catch; leave semantics are required and currently unsupported");
        }
        // Append epilogue: [stloc result] [args] call hook [ldloc result] ret.
        var epi = new List<Instruction>();
        if (returnsValue) { EnsureResultVar(); epi.Add(il.Create(OpCodes.Stloc, resultVar)); }
        epi.AddRange(BuildArgLoads(inPostfixEpilogue: true));
        epi.Add(il.Create(OpCodes.Call, hookRef));
        if (returnsValue) epi.Add(il.Create(OpCodes.Ldloc, resultVar));
        epi.Add(il.Create(OpCodes.Ret));
        var epiHead = epi[0];
        foreach (var ins in epi) body.Instructions.Add(ins);
        foreach (var r in rets) { r.OpCode = OpCodes.Br; r.Operand = epiHead; }
    }

    body.OptimizeMacros();
}

static VariableDefinition AddLocal(MethodBody body, TypeReference type)
{
    var v = new VariableDefinition(type);
    body.Variables.Add(v);
    return v;
}

static bool InRange(MethodBody body, Instruction ins, Instruction start, Instruction end)
{
    if (start == null) return false;
    int i = body.Instructions.IndexOf(ins), s = body.Instructions.IndexOf(start);
    int e = end == null ? body.Instructions.Count : body.Instructions.IndexOf(end);
    return i >= s && i < e;
}

record Manifest(List<PatchEntry> Patches, List<StripEntry>? StripReadonly = null, List<ConstEntry>? PatchConsts = null, List<RedirectEntry>? RedirectCalls = null);
// GenericArg is optional: match a generic call such as NextItem<NEventOptionButton> by fully qualified argument type.
record RedirectEntry(string NamespacePrefix, string TargetType, string TargetMethod, string HookType, string HookMethod, string? GenericArg = null);
record StripEntry(string Type, List<string> Fields);
// MethodContains matches method or nested-type names, including async state machines such as <PlayRunAsync>d__N.
record ConstEntry(string Type, int FromInt, int ToInt, string? MethodContains = null);
// TargetParams optionally disambiguates overloads by simple/full parameter type names; null requires a unique method name.
record PatchEntry(string TargetType, string TargetMethod, string Kind, string HookType, string HookMethod, List<string>? TargetParams = null);
