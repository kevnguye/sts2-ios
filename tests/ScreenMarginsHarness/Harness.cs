using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using STS2MobileIos;

public partial class Harness : Node
{
    private void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        GD.Print("PASS " + name);
    }
    public override async void _Ready()
    {
        string folder = Path.Combine(Path.GetTempPath(), "sts2-margins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "preferences");
            var store = new ScreenMarginSettings(path);
            Check(store.Horizontal == 0 && store.Vertical == 0, "fresh default has no insets");
            store.Set(10, 15);
            store = new ScreenMarginSettings(path);
            Check(store.Horizontal == 10 && store.Vertical == 15, "axes persist independently");
            bool rejected = false;
            try { store.Set(16, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && store.Horizontal == 10 && store.Vertical == 15, "invalid value preserves settings");
            File.WriteAllText(path, "1,2,garbage");
            store = new ScreenMarginSettings(path);
            Check(store.Horizontal == 0 && store.Vertical == 0, "corrupt pair resets both axes safely");
            Directory.CreateDirectory(path + ".blocked");
            var blocked = new ScreenMarginSettings(path + ".blocked");
            rejected = false;
            try { blocked.Set(5, 5); } catch (IOException) { rejected = true; }
            Check(rejected && blocked.Horizontal == 0, "failed disk write preserves active state");

            GetTree().Root.Size = new Vector2I(800,600);
            ScreenMargins.Start();
            ScreenMargins.Set(true, 10); ScreenMargins.Set(false, 15);
            var window = GetTree().Root;
            var size = window.GetVisibleRect().Size;
            var transform = window.GlobalCanvasTransform;
            Check((transform * Vector2.Zero).DistanceTo(size * new Vector2(.10f,.15f)) < .01f, "top/left inset");
            Check((transform * size).DistanceTo(size * new Vector2(.90f,.85f)) < .01f, "bottom/right inset");
            var layer = new CanvasLayer(); AddChild(layer);
            var button = new Button { Position = new Vector2(550,450), Size = new Vector2(80,40), Text = "test" };
            layer.AddChild(button);
            bool pressed = false; button.Pressed += () => pressed = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var logical = new Vector2(590,470);
            var screen = transform * logical;
            window.PushInput(new InputEventMouseMotion { Position = screen, GlobalPosition = screen }, false);
            window.PushInput(new InputEventMouseButton { Position = screen, GlobalPosition = screen, ButtonIndex = MouseButton.Left, Pressed = true }, false);
            window.PushInput(new InputEventMouseButton { Position = screen, GlobalPosition = screen, ButtonIndex = MouseButton.Left, Pressed = false }, false);
            Check(pressed, "GUI hit testing follows the inset canvas on a CanvasLayer");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = screen, GlobalPosition = screen });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var raw = window.GetMousePosition();
            Check(raw.DistanceTo(logical) < .01f, "Godot raw drag polling maps to canvas coordinates");
            var touchPoint = transform * new Vector2(610, 475);
            Input.ParseInputEvent(new InputEventScreenTouch { Index = 0, Position = touchPoint, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(window.GetMousePosition().DistanceTo(new Vector2(610, 475)) < .01f,
                "emulated touch polling follows the inset frame");
            Input.ParseInputEvent(new InputEventScreenTouch { Index = 0, Position = touchPoint, Pressed = false });
            window.Size = new Vector2I(1200,800);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check((window.GlobalCanvasTransform * Vector2.Zero).DistanceTo(new Vector2(120,120)) < .01f,
                "resize reapplies margins to the new frame");
            var viewport = new SubViewport { Size = new Vector2I(64,64) }; AddChild(viewport);
            Check(viewport.GlobalCanvasTransform == Transform2D.Identity, "subviewport rendering stays local");
            ScreenMargins.Set(true,0); ScreenMargins.Set(false,0);
            Check(window.GlobalCanvasTransform == Transform2D.Identity, "zero resets full frame and pointer mapping");
            GD.Print("Screen margins harness PASS"); GetTree().Quit(0);
        }
        catch (Exception e) { GD.PrintErr("FAIL " + e); GetTree().Quit(1); }
        finally { Directory.Delete(folder, true); }
    }
}
