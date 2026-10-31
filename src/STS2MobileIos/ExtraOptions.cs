using System;
using System.IO;
using Godot;

namespace STS2MobileIos;

public static class ExtraOptions
{
    private static ExtraPreferencesStore _store;
    private static ExtraPreferencesStore Store => _store ??=
        new ExtraPreferencesStore(Path.Combine(OS.GetUserDataDir(), "mobile_extras.json"));
    public static event Action Changed;
    public static bool Enabled(ExtraFeature feature) => Store.IsEnabled(feature);

    public static bool Set(ExtraFeature feature, bool enabled)
    {
        try { Store.Set(feature, enabled); }
        catch (Exception e)
        {
            PatchHelper.Log($"[Extras] Setting was not saved: {e.Message}");
            return false;
        }
        PatchHelper.Log($"[Extras] {feature}={enabled}");
        Changed?.Invoke();
        return true;
    }
}
