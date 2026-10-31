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
            PatchHelper.Log($"[TimeScale] 全局时间缩放设为 {TARGET_SCALE}x");
            PerformanceDiagnostics.Start();
            ScreenMargins.Start();
        }
        catch (Exception e)
        {
            PatchHelper.Log($"[TimeScale] 设置失败: {e.Message}");
        }
        try
        {
            // 内存额度自检(诊断用, 默认关闭): 容器 Documents 放一个 memcheck_on 文件即开启,
            // 删除即关闭(文件App可控, 无需进游戏)。开启时启动记录一行到 memcheck.txt。
            // os_proc_available_memory = 系统还允许本进程用多少内存;
            // 有 increased-memory-limit 权限 ≈ 7GB+, 没有 ≈ 3GB — 签名是否保住权限一眼可判。
            var docs = OS.GetUserDataDir();
            if (File.Exists(Path.Combine(docs, "memcheck_on")))
            {
                var line =
                    $"{DateTime.Now:MM-dd HH:mm:ss} 剩余内存额度 {OsProcAvailableMemory() / 1048576} MB";
                PatchHelper.Log($"[MemCheck] {line}");
                File.AppendAllText(Path.Combine(docs, "memcheck.txt"), line + "\n");
            }
        }
        catch (Exception e)
        {
            PatchHelper.Log($"[MemCheck] 自检失败: {e.Message}");
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
            PatchHelper.Log($"[TimeScale] HitStop 叠加失败: {e.Message}");
        }
        return false;
    }
}
