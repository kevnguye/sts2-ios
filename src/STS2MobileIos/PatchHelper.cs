using System;
using System.Reflection;
using System.IO;
using Godot;

namespace STS2MobileIos;

// Logging + reflection helpers for the statically-woven iOS patch set.
// Field()/Method() mirror HarmonyLib.AccessTools semantics: search the type
// AND its base types with all binding flags (plain Type.GetField does NOT
// return private fields declared on base classes).
public static class PatchHelper
{
    private static string _diagnosticsPath;
    private static bool _diagnosticsChecked;
    private static readonly object LogLock = new();
    public static bool DiagnosticsEnabled
    {
        get
        {
            if (!_diagnosticsChecked)
            {
                try
                {
                    string root = OS.GetUserDataDir();
                    string flag = Path.Combine(root, "mobile_diagnostics_on");
                    if (File.Exists(flag) && File.ReadAllText(flag).Trim() == "1")
                        _diagnosticsPath = Path.Combine(root, "mobile_diagnostics.log");
                    _diagnosticsChecked = true;
                }
                catch { }
            }
            return _diagnosticsPath != null;
        }
    }
    private const BindingFlags AllFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static FieldInfo Field(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, AllFlags | BindingFlags.DeclaredOnly);
            if (f != null)
                return f;
        }
        return null;
    }

    public static MethodInfo Method(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var m = t.GetMethod(name, AllFlags | BindingFlags.DeclaredOnly);
            if (m != null)
                return m;
        }
        return null;
    }

    public static void Log(string msg)
    {
        Console.Error.WriteLine($"[STS2MobileIos] {msg}");
        if (!DiagnosticsEnabled) return;
        try
        {
            lock (LogLock)
            {
                if (File.Exists(_diagnosticsPath) && new FileInfo(_diagnosticsPath).Length > 1024 * 1024)
                    File.Move(_diagnosticsPath, _diagnosticsPath + ".previous", true);
                File.AppendAllText(_diagnosticsPath, $"{DateTime.UtcNow:O} {msg}\n");
            }
        }
        catch { } // Diagnostics must never interrupt gameplay or save operations.
    }
}
