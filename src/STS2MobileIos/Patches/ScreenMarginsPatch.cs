using System;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace STS2MobileIos.Patches;

public static class ScreenMarginsPatch
{
    private const string AxisMeta = "mobile_screen_margin_axis";
    public static void SettingsLabelsPostfix(object __instance)
    {
        try
        {
            ScreenMargins.Start();
            var screen = (Node)__instance;
            var panel = screen.GetNode<NSettingsPanel>("%GraphicsSettings");
            var content = panel.Content;
            if (content.GetNodeOrNull("HorizontalScreenMargin") != null) return;
            var sound = screen.GetNode<NSettingsPanel>("%SoundSettings");
            var template = sound.Content.GetNode<Control>("MasterVolume");
            int index = content.GetNode("ResetGraphics").GetIndex();
            foreach (bool horizontal in new[] { true, false })
            {
                var row = (Control)template.Duplicate(14);
                row.Name = horizontal ? "HorizontalScreenMargin" : "VerticalScreenMargin";
                row.TooltipText = horizontal
                    ? "Inset the game from both left and right edges. 0% restores full width."
                    : "Inset the game from top and bottom to move cards away from the Home gesture. 0% restores full height.";
                row.GetNode<MegaRichTextLabel>("Label").Text = horizontal ? "Horizontal Screen Margin" : "Vertical Screen Margin";
                var slider = row.GetNode<NMasterVolumeSlider>("MasterVolumeSlider");
                slider.SetMeta(AxisMeta, horizontal);
                content.AddChild(row);
                content.MoveChild(row, index++);
            }
            panel.CallDeferred("RefreshSize");
            panel.CallDeferred("UpdateNavigation");
        }
        catch (Exception e) { PatchHelper.Log($"[ScreenMargins] Settings failed: {e}"); }
    }
    public static bool SliderReadyPrefix(object __instance)
    {
        var control = (Control)__instance;
        if (!control.HasMeta(AxisMeta)) return true;
        control.Call("ConnectSignals");
        bool horizontal = control.GetMeta(AxisMeta).AsBool();
        var slider = control.GetNode<Godot.Range>("Slider");
        slider.MinValue = 0; slider.MaxValue = ScreenMarginSettings.Maximum; slider.Step = 1;
        slider.Value = horizontal ? ScreenMargins.Settings.Horizontal : ScreenMargins.Settings.Vertical;
        control.GetNode<MegaLabel>("SliderValue").Call("SetTextAutoSize", $"{slider.Value}%");
        slider.ValueChanged += value =>
        {
            try { ScreenMargins.Set(horizontal, (int)Math.Round(value)); }
            catch (Exception e)
            {
                PatchHelper.Log($"[ScreenMargins] Saving failed: {e.Message}");
                slider.Value = horizontal ? ScreenMargins.Settings.Horizontal : ScreenMargins.Settings.Vertical;
            }
        };
        return false; // Do not connect the native master-volume callback.
    }
}
