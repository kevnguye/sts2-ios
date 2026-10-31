using STS2MobileIos;

int passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
    passed++;
}
string root = Path.Combine(Path.GetTempPath(), "sts2-extra-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string path = Path.Combine(root, "mobile_extras.json");
try
{
    var store = new ExtraPreferencesStore(path);
    Check(Enum.GetValues<ExtraFeature>().All(f => !store.IsEnabled(f)), "new installs default every extra off");
    Check(!File.Exists(path), "reading defaults does not write settings");
    foreach (var feature in Enum.GetValues<ExtraFeature>()) store.Set(feature, true);
    Check(Enum.GetValues<ExtraFeature>().All(f => new ExtraPreferencesStore(path).IsEnabled(f)), "all options survive restart");
    store.Set(ExtraFeature.SaveSlots, false);
    var restored = new ExtraPreferencesStore(path);
    Check(!restored.IsEnabled(ExtraFeature.SaveSlots) && restored.IsEnabled(ExtraFeature.CardSuggestions)
        && restored.IsEnabled(ExtraFeature.RestartRoom), "options are independent");
    File.WriteAllText(path, "{broken");
    Check(Enum.GetValues<ExtraFeature>().All(f => !new ExtraPreferencesStore(path).IsEnabled(f)), "corrupt preferences fail closed");
    File.WriteAllText(path, "{\"version\":2,\"save_slots\":true}");
    Check(!new ExtraPreferencesStore(path).IsEnabled(ExtraFeature.SaveSlots), "unsupported version fails closed");
    File.WriteAllText(path, "{\"version\":1,\"save_slots\":true,\"card_suggestions\":\"true\"}");
    Check(Enum.GetValues<ExtraFeature>().All(f => !new ExtraPreferencesStore(path).IsEnabled(f)), "bad boolean does not partially enable features");
    File.WriteAllText(path, "{\"version\":1,\"restart_room\":true,\"future_option\":true}");
    var future = new ExtraPreferencesStore(path);
    Check(future.IsEnabled(ExtraFeature.RestartRoom) && !future.IsEnabled(ExtraFeature.SaveSlots), "unknown fields do not enable missing options");
    File.Delete(path);
    Directory.CreateDirectory(path); // Force replacement to fail without changing permissions.
    bool failed = false;
    try { future.Set(ExtraFeature.SaveSlots, true); } catch (IOException) { failed = true; }
    Check(failed && !future.IsEnabled(ExtraFeature.SaveSlots) && future.IsEnabled(ExtraFeature.RestartRoom), "failed persistence keeps previous in-memory values");
    Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "failed persistence removes temporary files");
    Check(Directory.Exists(path), "failed persistence does not remove blocking data");
    Console.WriteLine($"{passed} preference checks passed");
}
finally { Directory.Delete(root, true); }
