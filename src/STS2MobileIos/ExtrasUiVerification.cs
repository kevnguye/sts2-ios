using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace STS2MobileIos;

// One-shot, explicitly requested device verification; absent flag means no work.
// Uses real native checkbox callbacks, restores choices and captures the result.
public static class ExtrasUiVerification
{
    public static async Task Run(Node screen, VBoxContainer content)
    {
        string root = OS.GetUserDataDir();
        string flag = Path.Combine(root, "mobile_extras_ui_test");
        if (!File.Exists(flag)) return;
        File.Delete(flag);
        var features = Enum.GetValues<ExtraFeature>();
        var before = features.Select(ExtraOptions.Enabled).ToArray();
        try
        {
            var tree = screen.GetTree();
            for (int i = 0; i < 10; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            while (screen is CanvasItem item && !item.IsVisibleInTree())
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            bool runTimer = SaveManager.Instance.PrefsSave.ShowRunTimer;
            foreach (var feature in features)
            {
                var box = content.GetNode<NRunTimerTickbox>("Extra" + feature + "/SettingsTickbox");
                bool previous = box.IsTicked;
                if (previous != ExtraOptions.Enabled(feature)) throw new Exception("Initial checkbox mismatch: " + feature);
                box.ForceToggleTick();
                if (box.IsTicked != !previous || ExtraOptions.Enabled(feature) != !previous)
                    throw new Exception("Toggle failed: " + feature);
                box.ForceToggleTick();
                if (box.IsTicked != previous || ExtraOptions.Enabled(feature) != previous)
                    throw new Exception("Toggle restore failed: " + feature);
                if (SaveManager.Instance.PrefsSave.ShowRunTimer != runTimer)
                    throw new Exception("Native run timer setting was modified");
                if (new ExtraPreferencesStore(Path.Combine(root, "mobile_extras.json")).IsEnabled(feature) != previous)
                    throw new Exception("Persisted setting mismatch: " + feature);
            }
            content.GetNode<Control>("ExtraRestartRoom/SettingsTickbox").GrabFocus();
            for (int i = 0; i < 45; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = screen.GetViewport().GetTexture().GetImage();
            image.SavePng(Path.Combine(root, "extras-settings-verification.png"));
            File.WriteAllText(Path.Combine(root, "extras-ui-verification.txt"),
                "PASS: 3 native checkbox on/off round trips, persisted preferences, native run-timer unchanged.\n" +
                string.Join("\n", features.Select(f => f + "=" + ExtraOptions.Enabled(f))));
            PatchHelper.Log("[Extras] Native checkbox verification passed");
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(root, "extras-ui-verification.txt"), "FAIL: " + e);
            PatchHelper.Log("[Extras] Native checkbox verification failed: " + e);
        }
        finally
        {
            for (int i = 0; i < features.Length; i++)
                if (ExtraOptions.Enabled(features[i]) != before[i]) ExtraOptions.Set(features[i], before[i]);
        }
    }
}
