using System;
using System.Diagnostics;
using Godot;

namespace STS2MobileIos;

// Opt-in, bounded file diagnostics. No custom Godot node/script registration is
// required: the existing SceneTree emits the callback. Disabled without a flag.
public static class PerformanceDiagnostics
{
    private static bool _started;
    private static long _last, _window;
    private static int _frames, _slowFrames;
    private static double _maxMs;

    public static void Start()
    {
        if (_started || !PatchHelper.DiagnosticsEnabled) return;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        _started = true;
        _last = _window = Stopwatch.GetTimestamp();
        tree.ProcessFrame += OnFrame;
        PatchHelper.Log($"[Performance] started; renderer={RenderingServer.GetCurrentRenderingMethod()}, driver={RenderingServer.GetCurrentRenderingDriverName()}, timeScale={Engine.TimeScale}");
    }

    private static void OnFrame()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = (now - _last) * 1000.0 / Stopwatch.Frequency;
        _last = now;
        _frames++;
        if (elapsed > 100) _slowFrames++;
        _maxMs = Math.Max(_maxMs, elapsed);
        double windowSeconds = (now - _window) / (double)Stopwatch.Frequency;
        if (windowSeconds < 5) return;
        PatchHelper.Log($"[Performance] fps={_frames / windowSeconds:F1}, maxFrameMs={_maxMs:F1}, framesOver100ms={_slowFrames}, timeScale={Engine.TimeScale:F2}");
        _window = now;
        _frames = _slowFrames = 0;
        _maxMs = 0;
    }
}
