using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace STS2MobileIos.Patches;

// Precompile shaders on first launch to reduce stalls when new visuals appear; coverage is not guaranteed.
// Ported from Android ShaderWarmupScreen, with three iOS adaptations:
// 1. Do not inherit Node/Control: this separate STS2MobileIos assembly is outside the main assembly's
// script registry, so node lifecycle callbacks would not run. Use static logic and SceneTree signals instead.
// 2. Remove the Launcher.Components progress panel and log progress through PatchHelper.Log.
// 3. Start from NGame._EnterTree after SceneTree/resources exist; run asynchronously without blocking startup.
// Android used a blocking warmup screen. Here, a marker skips later attempts after the first warmup.
public static class ShaderWarmupPatch
{
    private const int WarmupVersion = 5;
    private const int BatchSize = 8;
    private static bool _started = false;

    // postfix on MegaCrit.Sts2.Core.Nodes.NGame._EnterTree
    public static void EnterTreePostfix(Node __instance)
    {
        if (_started)
            return;
        _started = true;
        try
        {
            if (!NeedsWarmup())
            {
                PatchHelper.Log("[ShaderWarmup] Already warmed up; skipping");
                return;
            }
            var tree = __instance.GetTree();
            if (tree == null)
            {
                PatchHelper.Log("[ShaderWarmup] SceneTree unavailable; skipping");
                return;
            }
            // Start asynchronous warmup without awaiting it here.
            _ = RunWarmup(tree);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Start failed: {ex.Message}");
        }
    }

    private static bool NeedsWarmup()
    {
        try
        {
            var markerPath = Path.Combine(OS.GetUserDataDir(), "shader_warmup_version");
            if (File.Exists(markerPath))
                return File.ReadAllText(markerPath).Trim() != WarmupVersion.ToString();
            return true;
        }
        catch
        {
            return true;
        }
    }

    private static void WriteVersionMarker()
    {
        try
        {
            File.WriteAllText(
                Path.Combine(OS.GetUserDataDir(), "shader_warmup_version"),
                WarmupVersion.ToString()
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Writing marker failed: {ex.Message}");
        }
    }

    private static async Task RunWarmup(SceneTree tree)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Wait for the main menu to load/display to reduce startup resource contention.
            for (int f = 0; f < 30; f++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(
                RenderingServer.Singleton,
                RenderingServer.SignalName.FramePostDraw
            );

            var materials = await CollectMaterialsAsync(tree);
            PatchHelper.Log($"[ShaderWarmup] Collected {materials.Count} shaders to compile");

            if (materials.Count == 0)
            {
                WriteVersionMarker();
                return;
            }

            var viewport = new SubViewport
            {
                Size = new Vector2I(64, 64),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = true,
            };
            tree.Root.AddChild(viewport);

            var whiteImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            whiteImage.SetPixel(0, 0, Colors.White);
            var whiteTex = ImageTexture.CreateFromImage(whiteImage);

            int total = materials.Count;
            for (int i = 0; i < total; i += BatchSize)
            {
                var batchNodes = new List<Node>();
                int batchEnd = Math.Min(i + BatchSize, total);
                for (int j = i; j < batchEnd; j++)
                {
                    try
                    {
                        var node = CreateWarmupNode(materials[j].mat, whiteTex);
                        if (node != null)
                        {
                            viewport.AddChild(node);
                            batchNodes.Add(node);
                        }
                    }
                    catch (Exception ex)
                    {
                        PatchHelper.Log($"[ShaderWarmup] Node creation failed for {materials[j].path}: {ex.Message}");
                    }
                }

                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

                foreach (var node in batchNodes)
                    node.QueueFree();

                if (i % 64 == 0)
                    PatchHelper.Log($"[ShaderWarmup] Progress {batchEnd}/{total}");
            }

            viewport.QueueFree();
            WriteVersionMarker();
            PatchHelper.Log(
                $"[ShaderWarmup] Complete: {total} shaders in {sw.ElapsedMilliseconds}ms. Later launches will skip warmup."
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Warmup failed: {ex}");
        }
    }

    private static Node CreateWarmupNode(Material mat, ImageTexture whiteTex)
    {
        if (mat is ParticleProcessMaterial particleMat)
        {
            return new GpuParticles2D
            {
                ProcessMaterial = particleMat,
                Amount = 1,
                Emitting = true,
                OneShot = false,
                Texture = whiteTex,
            };
        }
        return new Sprite2D { Texture = whiteTex, Material = mat };
    }

    // Yield a frame after every N resources to avoid >10-second main-thread stalls and the iOS watchdog.
    private const int YieldEvery = 4;

    private static async Task<List<(string path, Material mat)>> CollectMaterialsAsync(
        SceneTree tree
    )
    {
        // Deduplicate shaders immediately after loading each material, while it is still alive.
        // Do not collect everything into a dictionary and calculate keys at the end: Godot can evict resources
        // between yielded frames, causing GetShaderKey to throw ObjectDisposedException and abort warmup.
        var unique = new Dictionary<string, (string path, Material mat)>();
        int seen = 0;

        // 1. Collect paths cheaply, then load asynchronously and deduplicate immediately, yielding frequently.
        var matPaths = new List<string>();
        CollectMaterialPaths("res://", matPaths);
        PatchHelper.Log($"[ShaderWarmup] Scanning {matPaths.Count} material/shader resources");
        for (int i = 0; i < matPaths.Count; i++)
        {
            if (LoadAndAdd(matPaths[i], unique))
                seen++;
            if (i % YieldEvery == 0)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        var scenePaths = new List<string>();
        CollectScenePaths("res://scenes", scenePaths);
        PatchHelper.Log($"[ShaderWarmup] Scanning {scenePaths.Count} scenes");

        for (int i = 0; i < scenePaths.Count; i++)
        {
            try
            {
                var packed = ResourceLoader.Load<PackedScene>(
                    scenePaths[i],
                    null,
                    ResourceLoader.CacheMode.Reuse
                );
                if (packed != null)
                    seen += ExtractMaterialsFromSceneState(packed, scenePaths[i], unique);
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[ShaderWarmup] Scene extraction failed for {scenePaths[i]}: {ex.Message}");
            }
            if (i % YieldEvery == 0)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        PatchHelper.Log($"[ShaderWarmup] {seen} materials -> {unique.Count} unique shaders");
        return unique.Values.ToList();
    }

    // Calculate the material key immediately and add it to unique; skip individual disposed/invalid resources.
    // Return whether the resource was counted, for statistics only.
    private static bool TryAddUnique(
        Dictionary<string, (string path, Material mat)> unique,
        string path,
        Material mat
    )
    {
        try
        {
            unique.TryAdd(GetShaderKey(mat), (path, mat));
            return true;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Deduplication skipped {path}: {ex.Message}");
            return false;
        }
    }

    private static string GetShaderKey(Material mat)
    {
        if (mat is ShaderMaterial sm && sm.Shader != null)
            return sm.Shader.ResourcePath ?? sm.Shader.GetRid().ToString();
        if (mat is ParticleProcessMaterial)
            return $"particle#{mat.GetRid()}";
        return mat.ResourcePath ?? mat.GetRid().ToString();
    }

    // List candidate paths without loading; the asynchronous loop loads them with frequent frame yields.
    private static void CollectMaterialPaths(string dirPath, List<string> outPaths)
    {
        try
        {
            using var dir = DirAccess.Open(dirPath);
            if (dir == null)
                return;
            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (fileName == "." || fileName == "..")
                    continue;
                var fullPath = $"{dirPath}/{fileName}";
                if (dir.CurrentIsDir())
                {
                    if (fileName == "debug")
                        continue;
                    CollectMaterialPaths(fullPath, outPaths);
                    continue;
                }
                var cleanName = fileName.Replace(".remap", "");
                if (
                    !cleanName.EndsWith(".tres")
                    && !cleanName.EndsWith(".gdshader")
                    && !cleanName.EndsWith(".material")
                )
                    continue;
                outPaths.Add($"{dirPath}/{cleanName}");
            }
            dir.ListDirEnd();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Enumeration failed for {dirPath}: {ex.Message}");
        }
    }

    // Load one material/shader and add its key to unique while alive. Return whether it was counted.
    private static bool LoadAndAdd(
        string cleanPath,
        Dictionary<string, (string path, Material mat)> unique
    )
    {
        try
        {
            if (!ResourceLoader.Exists(cleanPath))
                return false;
            Material mat = null;
            if (cleanPath.EndsWith(".tres"))
            {
                mat =
                    ResourceLoader.Load(cleanPath, "Material", ResourceLoader.CacheMode.Reuse)
                    as Material;
                if (mat == null)
                {
                    var shader =
                        ResourceLoader.Load(cleanPath, "Shader", ResourceLoader.CacheMode.Reuse)
                        as Shader;
                    if (shader != null)
                        mat = new ShaderMaterial { Shader = shader };
                }
            }
            else
            {
                var res = ResourceLoader.Load(cleanPath, null, ResourceLoader.CacheMode.Reuse);
                if (res is Material resMat)
                    mat = resMat;
                else if (res is Shader resShader)
                    mat = new ShaderMaterial { Shader = resShader };
            }
            if (mat != null)
                return TryAddUnique(unique, cleanPath, mat);
            return false;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Loading failed for {cleanPath}: {ex.Message}");
            return false;
        }
    }

    private static void CollectScenePaths(string dirPath, List<string> paths)
    {
        try
        {
            using var dir = DirAccess.Open(dirPath);
            if (dir == null)
                return;
            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (fileName == "." || fileName == "..")
                    continue;
                var fullPath = $"{dirPath}/{fileName}";
                if (dir.CurrentIsDir())
                {
                    if (fileName == "debug")
                        continue;
                    CollectScenePaths(fullPath, paths);
                    continue;
                }
                var cleanName = fileName.Replace(".remap", "");
                if (!cleanName.EndsWith(".tscn"))
                    continue;
                var cleanPath = $"{dirPath}/{cleanName}";
                if (ResourceLoader.Exists(cleanPath))
                    paths.Add(cleanPath);
            }
            dir.ListDirEnd();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[ShaderWarmup] Scene enumeration failed for {dirPath}: {ex.Message}");
        }
    }

    // Extract scene materials and add each key to unique immediately. Return the number counted.
    private static int ExtractMaterialsFromSceneState(
        PackedScene packed,
        string scenePath,
        Dictionary<string, (string path, Material mat)> unique
    )
    {
        int added = 0;
        var state = packed.GetState();
        int nodeCount = state.GetNodeCount();
        for (int n = 0; n < nodeCount; n++)
        {
            int propCount = state.GetNodePropertyCount(n);
            for (int p = 0; p < propCount; p++)
            {
                var propName = state.GetNodePropertyName(n, p).ToString();
                if (
                    propName != "material"
                    && propName != "process_material"
                    && propName != "surface_material_override/0"
                )
                    continue;
                try
                {
                    var val = state.GetNodePropertyValue(n, p);
                    Material mat = null;
                    if (val.Obj is Material m)
                        mat = m;
                    else if (val.Obj is Shader shader)
                        mat = new ShaderMaterial { Shader = shader };
                    if (mat != null && TryAddUnique(unique, $"{scenePath}#node{n}#{propName}", mat))
                        added++;
                }
                catch (Exception ex)
                {
                    PatchHelper.Log($"[ShaderWarmup] Reading property {propName}@{scenePath} failed: {ex.Message}");
                }
            }
        }
        return added;
    }
}
