using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace STS2MobileIos.Patches;

// Reuse the owned game's native row, tickbox, sounds, focus and help-tooltip UI.
// The metadata-scoped hooks leave the real Show Run Timer preference untouched.
public static class ExtrasSettingsPatch
{
    private const string FeatureMeta = "mobile_extra_feature";
    private static bool _subscribed;

    public static void SettingsLabelsPostfix(object __instance)
    {
        try
        {
            var screen = (Node)__instance;
            var panel = screen.GetNode<NSettingsPanel>("%GeneralSettings");
            var content = panel.Content;
            if (content.GetNodeOrNull("ExtrasHeader") != null) return;
            if (!_subscribed)
            {
                ExtraOptions.Changed += RefreshExistingMenus;
                _subscribed = true;
            }
            var template = content.GetNode<Control>("ShowRunTimer");
            int index = content.GetNode("ResetGameplayDivider").GetIndex();
            var divider = content.GetNode("ResetGameplayDivider").Duplicate(14);
            divider.Name = "ExtrasDivider";
            divider.UniqueNameInOwner = false;
            content.AddChild(divider);
            content.MoveChild(divider, index++);

            var header = (Control)template.Duplicate(14);
            header.Name = "ExtrasHeader";
            header.SetMeta(FeatureMeta, "header");
            var headerBox = header.GetNode("SettingsTickbox");
            header.RemoveChild(headerBox);
            headerBox.Free();
            header.GetNode<MegaRichTextLabel>("Label").Text = "[b]Extras[/b]";
            content.AddChild(header);
            content.MoveChild(header, index++);

            foreach (ExtraFeature feature in Enum.GetValues<ExtraFeature>())
            {
                var row = (Control)template.Duplicate(14);
                row.Name = "Extra" + feature;
                row.SetMeta(FeatureMeta, feature.ToString());
                row.GetNode<MegaRichTextLabel>("Label").Text = Title(feature);
                var box = row.GetNode<NRunTimerTickbox>("SettingsTickbox");
                box.SetMeta(FeatureMeta, feature.ToString());
                content.AddChild(row); // The scoped Ready prefix initializes it.
                content.MoveChild(row, index++);
                // Also explain on checkbox focus/touch, using the native row tip.
                box.MouseEntered += () => row.Call("OnHovered");
                box.MouseExited += () => row.Call("OnUnhovered");
                box.FocusEntered += () => row.Call("OnHovered");
                box.FocusExited += () => row.Call("OnUnhovered");
            }
            panel.CallDeferred("RefreshSize");
            panel.CallDeferred("UpdateNavigation");
            PatchHelper.Log("[Extras] Added native settings rows");
            _ = ExtrasUiVerification.Run(screen, content);
        }
        catch (Exception e) { PatchHelper.Log($"[Extras] Settings UI failed: {e}"); }
    }

    public static bool TickboxReadyPrefix(object __instance)
    {
        if (!TryFeature(__instance, out var feature)) return true;
        var box = (NRunTimerTickbox)__instance;
        box.Call("ConnectSignals");
        box.IsTicked = ExtraOptions.Enabled(feature);
        return false;
    }

    public static bool SetFromSettingsPrefix(object __instance)
    {
        if (!TryFeature(__instance, out var feature)) return true;
        ((NTickbox)__instance).IsTicked = ExtraOptions.Enabled(feature);
        return false;
    }

    public static bool TickPrefix(object __instance) => Toggle(__instance, true);
    public static bool UntickPrefix(object __instance) => Toggle(__instance, false);
    private static bool Toggle(object instance, bool enabled)
    {
        if (!TryFeature(instance, out var feature)) return true;
        ExtraOptions.Set(feature, enabled);
        ((NTickbox)instance).IsTicked = ExtraOptions.Enabled(feature);
        return false;
    }

    public static void HoverTipReadyPostfix(object __instance)
    {
        var row = (Node)__instance;
        if (!row.HasMeta(FeatureMeta)) return;
        bool isFeature = TryFeature(row, out var feature);
        string title = isFeature ? Title(feature) : "Extras";
        string description = isFeature ? Description(feature) : "Optional port features. All are off by default.";
        object tip = new HoverTip(new LocString("settings_ui", "SHOW_RUN_TIMER_HEADER"), description)
            { Id = "mobile_extras." + row.GetMeta(FeatureMeta).AsString() };
        PatchHelper.Field(typeof(HoverTip), "<Title>k__BackingField").SetValue(tip, title);
        PatchHelper.Field(row.GetType(), "_hoverTip").SetValue(row, tip);
    }

    private static bool TryFeature(object instance, out ExtraFeature feature)
    {
        feature = default;
        return instance is Node node && node.HasMeta(FeatureMeta) &&
            Enum.TryParse(node.GetMeta(FeatureMeta).AsString(), out feature) && Enum.IsDefined(feature);
    }

    private static string Title(ExtraFeature feature) => feature switch
    {
        ExtraFeature.SaveSlots => "Run Save Slots",
        ExtraFeature.CardSuggestions => "Card Suggestions",
        _ => "Restart Room"
    };

    private static string Description(ExtraFeature feature) => feature switch
    {
        ExtraFeature.SaveSlots => "Adds three Save Slot and Load Slot pairs to the pause menu. They copy the latest autosave, not the exact mid-turn state. Turning this off keeps existing slots.",
        ExtraFeature.CardSuggestions => "Shows card ratings and suggestions in rewards, shops and card-selection screens. Runs locally and does not change game rules or card outcomes.",
        _ => "Adds Restart Room to the pause menu. Reloads the room's latest autosave so you can try it again."
    };

    private static void RefreshExistingMenus()
    {
        try
        {
            if (Engine.GetMainLoop() is SceneTree tree) Visit(tree.Root);
            CardAdvisorPatch.OptionsChanged();
        }
        catch (Exception e) { PatchHelper.Log($"[Extras] UI refresh failed: {e.Message}"); }
    }

    private static void Visit(Node node)
    {
        if (node is NPauseMenu && node.IsNodeReady()) QuickRestartPatch.ReadyPostfix(node);
        foreach (var child in node.GetChildren().ToArray()) Visit(child);
    }
}
