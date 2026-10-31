using System;
using System.IO;
using Godot;

namespace STS2MobileIos.Patches;

// Keep normal engine time. Hand-card interpolation uses delta * 7/8/10 without
// clamping; forcing 4x makes it overshoot even at otherwise playable frame rates.
// The game's own Fast Mode setting can shorten animations without accelerating
// every UI update and physics tick. Preserve the normal hit-stop time scale too.
public static class TimeScalePatch
{
    private const double TARGET_SCALE = 1.0;

    // postfix on MegaCrit.Sts2.Core.Nodes.NGame._Ready
    // Restore normal timing when the game root becomes ready.
    public static void ReadyPostfix()
    {
        try
        {
            Engine.TimeScale = TARGET_SCALE;
            PatchHelper.Log($"[TimeScale] Global time scale set to {TARGET_SCALE}x");
            PerformanceDiagnostics.Start();
        }
        catch (Exception e)
        {
            PatchHelper.Log($"[TimeScale] Setting time scale failed: {e.Message}");
        }
        try
        {
            // Optional memory-budget diagnostic: create memcheck_on in the app's Documents directory to enable it.
            // Delete it to disable logging; the Files app can manage it without launching the game. Append one startup line to memcheck.txt.
            // os_proc_available_memory reports the additional memory the process may currently allocate.
            // The original author observed ~7GB+ with increased-memory-limit versus ~3GB without; actual budgets vary by device and state.
            var docs = OS.GetUserDataDir();
            if (File.Exists(Path.Combine(docs, "memcheck_on")))
            {
                var line =
                    $"{DateTime.Now:MM-dd HH:mm:ss} Available memory budget: {OsProcAvailableMemory() / 1048576} MB";
                PatchHelper.Log($"[MemCheck] {line}");
                File.AppendAllText(Path.Combine(docs, "memcheck.txt"), line + "\n");
            }
        }
        catch (Exception e)
        {
            PatchHelper.Log($"[MemCheck] Diagnostic failed: {e.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport(
        "/usr/lib/libSystem.B.dylib",
        EntryPoint = "os_proc_available_memory"
    )]
    private static extern ulong OsProcAvailableMemory();

    // Preserve the game's hit-stop values and its eventual return to normal time.
    public static bool HitStopSetTimeScalePrefix(float timeScale)
    {
        try
        {
            Engine.TimeScale = timeScale * TARGET_SCALE;
        }
        catch (Exception e)
        {
            PatchHelper.Log($"[TimeScale] Applying hit-stop scale failed: {e.Message}");
        }
        return false;
    }
}
