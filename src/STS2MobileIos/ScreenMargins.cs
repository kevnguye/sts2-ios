using System;
using System.IO;
using Godot;

namespace STS2MobileIos;

public static class ScreenMargins
{
    private static ScreenMarginSettings _settings;
    private static Window _window;
    public static ScreenMarginSettings Settings => _settings ??= new ScreenMarginSettings(
        Path.Combine(OS.GetUserDataDir(), "mobile_screen_margins.cfg"));
    public static void Start()
    {
        if (_window != null) return;
        _window = ((SceneTree)Engine.GetMainLoop()).Root;
        _window.SizeChanged += Apply;
        Apply();
    }
    public static void Set(bool horizontal, int value)
    {
        Settings.Set(horizontal ? value : Settings.Horizontal, horizontal ? Settings.Vertical : value);
        Apply();
    }
    public static void Apply()
    {
        if (_window == null) return;
        var size = _window.GetVisibleRect().Size;
        float x = Settings.Horizontal / 100f, y = Settings.Vertical / 100f;
        _window.GlobalCanvasTransform = new Transform2D(
            new Vector2(1 - 2*x, 0), new Vector2(0, 1 - 2*y), new Vector2(size.X*x, size.Y*y));
    }
    // Godot's final viewport transform maps both GUI events and raw mouse
    // polling back into logical game coordinates, including emulated touch.
}
