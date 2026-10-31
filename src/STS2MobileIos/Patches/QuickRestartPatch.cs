using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace STS2MobileIos.Patches;

// Adds a "Restart Room" button to the in-game pause menu (mobile equivalent of the
// Steam mod "Quick Restart 2"): reloads the most recent run save, dropping you back
// into the current room as it was when you entered it. Core convenience for save-scum
// ("sl") play on mobile where quitting to the main menu and hitting Continue by hand
// is tedious.
//
// Reload strategy — reuse the game's own, fully-tested reload path (the exact sequence
// a player performs manually as "Save & Quit is NOT used" -> quit to menu -> Continue):
//   1. NGame.ReturnToMainMenu(): fades to black, RunManager.CleanUp() (which clears
//      RunState so a fresh load is allowed), and loads the main menu behind the black
//      overlay. CRITICALLY, this does NOT write a save, so the on-disk checkpoint (the
//      room-entry autosave) is preserved untouched — this is what makes "restart room"
//      actually restart the room instead of persisting the messed-up current state.
//   2. NMainMenu.RefreshButtons(): re-reads the run save from disk into _readRunSaveResult.
//   3. Invoke NMainMenu.OnContinueButtonPressed(null): the identical code the Continue
//      button runs — SetUpSavedSingleplayer (increments the reload counter), networking
//      setup, NGame.LoadRun(...) and a fade-in into the reloaded room.
// Because ReturnToMainMenu leaves the screen faded to black and OnContinueButtonPressed
// fades back in only after the room is loaded, there is no visible main-menu flash.
public static class QuickRestartPatch
{
    private const string ButtonName = "RestartRoom";

    // Postfix on NPauseMenu._Ready for iOS/mobile.
    // Add Restart Room and six save/load snapshot buttons when their respective Extras are enabled.
    public static void ReadyPostfix(object __instance)
    {
        try
        {
            var container = (Control)PatchHelper.Field(((Node)__instance).GetType(), "_buttonContainer")
                ?.GetValue(__instance);
            if (container == null) return;
            if (ExtraOptions.Enabled(ExtraFeature.RestartRoom)) AddRestartButton(__instance);
            else RemoveButton(container, ButtonName);
            var saveBtn = (Node)PatchHelper.Field(((Node)__instance).GetType(), "_saveAndQuitButton")
                ?.GetValue(__instance);
            SnapshotPatch.AddButtons(container, saveBtn);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[QuickRestart] Snapshot buttons failed: {ex}");
        }
    }

    // Desktop postfix on NPauseMenu._Ready: Restart Room only, without snapshot buttons.
    public static void RestartOnlyPostfix(object __instance)
    {
        AddRestartButton(__instance, false);
    }

    private static void RemoveButton(Node parent, string name)
    {
        var child = parent.GetNodeOrNull(name);
        if (child == null) return;
        parent.RemoveChild(child);
        child.QueueFree();
    }

    // Clone the native Save and Quit button to preserve its style/script, then attach the restart action.
    // Return the original saveBtn for snapshot-button reuse; return null on failure or if already present.
    private static Node AddRestartButton(object __instance, bool respectExtras = true)
    {
        try
        {
            var pauseMenu = (Node)__instance;

            var container = (Control)PatchHelper.Field(pauseMenu.GetType(), "_buttonContainer")
                ?.GetValue(pauseMenu);
            var saveBtn = (Node)PatchHelper.Field(pauseMenu.GetType(), "_saveAndQuitButton")
                ?.GetValue(pauseMenu);

            if (container == null || saveBtn == null)
            {
                PatchHelper.Log("[QuickRestart] _buttonContainer/_saveAndQuitButton not found");
                return null;
            }

            // Guard against adding the button twice if _Ready ever runs again.
            if (container.GetNodeOrNull(ButtonName) != null)
                return null;

            // Duplicate without DUPLICATE_SIGNALS (flag 1) so the original's code-made
            // Released -> Save&Quit connection is not copied. Keeps groups(2), scripts(4)
            // and scene instantiation(8) => flags = 14.
            var dup = saveBtn.Duplicate(14);
            dup.Name = ButtonName;
            dup.SetMeta("mobile_extra_restart", respectExtras);

            var label = dup.GetNodeOrNull<MegaLabel>("Label");
            if (label != null)
                label.SetTextAutoSize(RestartLabelText());

            container.AddChild(dup);
            // Place it directly under "Resume" (index 0) for quick reach during sl play.
            container.MoveChild(dup, 1);

            ((NClickableControl)dup).Released += OnRestartPressed;

            PatchHelper.Log("[QuickRestart] Added Restart Room button to pause menu");
            return saveBtn;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[QuickRestart] AddRestartButton failed: {ex}");
            return null;
        }
    }

    private static string RestartLabelText()
    {
        try
        {
            var locale = TranslationServer.GetLocale();
            if (!string.IsNullOrEmpty(locale) && locale.StartsWith("zh"))
                return "Restart Room";
        }
        catch
        {
            // fall through to English
        }
        return "Restart Room";
    }

    // Released signal handler (ReleasedEventHandler => one NClickableControl arg).
    private static void OnRestartPressed(NClickableControl _)
    {
        if (_.HasMeta("mobile_extra_restart") && _.GetMeta("mobile_extra_restart").AsBool() &&
            !ExtraOptions.Enabled(ExtraFeature.RestartRoom)) return;
        TaskHelper.RunSafely(ReloadCurrentRunAsync());
    }

    // Reloads the on-disk run save via the game's own, fully-tested path (the exact
    // sequence a player performs manually: quit to menu -> Continue). Shared by the
    // Restart Room button and by the snapshot "Load" button (which restores files
    // first, then reloads through here). No save is written along the way, so the
    // on-disk checkpoint is honoured verbatim.
    public static async Task ReloadCurrentRunAsync()
    {
        try
        {
            var game = NGame.Instance;
            if (game == null)
            {
                PatchHelper.Log("[QuickRestart] NGame.Instance is null");
                return;
            }

            // Tear down to the main menu behind a black fade WITHOUT saving, so the
            // on-disk checkpoint is preserved and RunState is cleared for a fresh load.
            await game.ReturnToMainMenu();

            var menu = game.MainMenu;
            if (menu == null)
            {
                PatchHelper.Log("[QuickRestart] MainMenu not available after ReturnToMainMenu");
                return;
            }

            // Re-read the run save from disk into the main menu's cached result.
            menu.RefreshButtons();

            // Run the exact Continue-button logic (private) to reload the run.
            var mi = PatchHelper.Method(menu.GetType(), "OnContinueButtonPressed");
            if (mi == null)
            {
                PatchHelper.Log("[QuickRestart] OnContinueButtonPressed not found");
                return;
            }

            mi.Invoke(menu, new object[] { null });
            PatchHelper.Log("[QuickRestart] Reload triggered via Continue path");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[QuickRestart] ReloadCurrentRunAsync failed: {ex}");
        }
    }
}
