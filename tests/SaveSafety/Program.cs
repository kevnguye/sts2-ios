using System.Diagnostics;
using System.Security.Cryptography;
using STS2MobileIos;
using STS2MobileIos.Patches;

string repo = Path.GetFullPath(args[0]);
int passed = 0, failed = 0;
void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Test(string name, Action<string> body)
{
    string directory = Path.Combine(Path.GetTempPath(), "sts2-save-test-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    Godot.OS.TestRoot = directory;
    try { body(directory); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
    finally { Directory.Delete(directory, true); }
}
void Save(string directory, string contents, int age = 100)
{
    Directory.CreateDirectory(directory);
    string file = Path.Combine(directory, "profile.save");
    File.WriteAllText(file, contents);
    File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(-age));
}
string Transfer(string directory)
{
    string source = Path.Combine(directory, "desktop");
    Save(source, "new-profile", 10);
    Directory.CreateDirectory(Path.Combine(source, "runs"));
    File.WriteAllText(Path.Combine(source, "runs", "run.save"), "run-data");
    File.SetLastWriteTimeUtc(Path.Combine(source, "runs", "run.save"), DateTime.UtcNow.AddSeconds(-10));
    string transfer = Path.Combine(directory, "sync_inbox", "test-transfer");
    var info = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (string argument in new[] { Path.Combine(repo, "tools/save_manifest.py"), "stage", source, transfer })
        info.ArgumentList.Add(argument);
    using var process = Process.Start(info);
    string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    Assert(process.ExitCode == 0, "Python sender failed: " + error + output);
    File.WriteAllText(Path.Combine(transfer, "complete"),
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(transfer, "manifest.json")))).ToLowerInvariant());
    return transfer;
}
void Unchanged(string directory, Action<string> damage)
{
    Save(Path.Combine(directory, "default", "1"), "original");
    string transfer = Transfer(directory);
    damage(transfer);
    SyncImportPatch.TryImport();
    Assert(File.ReadAllText(Path.Combine(directory, "default", "1", "profile.save")) == "original", "Invalid transfer replaced saves");
    Assert(Directory.Exists(transfer), "Invalid transfer was deleted");
}
Test("Python manifest imports and preserves other profiles", d => {
    Save(Path.Combine(d, "default", "1"), "old-profile");
    Save(Path.Combine(d, "default", "2"), "other-profile");
    string transfer = Transfer(d);
    Assert(SyncImportPatch.ValidateTransfer(transfer) > 0, "Python/C# manifest mismatch");
    SyncImportPatch.TryImport();
    Assert(File.ReadAllText(Path.Combine(d, "default", "1", "profile.save")) == "new-profile", "Import missing");
    Assert(File.ReadAllText(Path.Combine(d, "default", "2", "profile.save")) == "other-profile", "Other profile overwritten");
    Assert(File.ReadAllText(Path.Combine(d, "default", "1.previous", "profile.save")) == "old-profile", "Backup lost");
});
Test("Missing completion marker rejected", d => Unchanged(d, t => File.Delete(Path.Combine(t, "complete"))));
Test("Missing profile rejected", d => Unchanged(d, t => File.Delete(Path.Combine(t, "1", "profile.save"))));
Test("Truncated transfer rejected", d => Unchanged(d, t => File.WriteAllText(Path.Combine(t, "1", "runs", "run.save"), "")));
Test("Unexpected extra file rejected", d => Unchanged(d, t => File.WriteAllText(Path.Combine(t, "1", "extra"), "x")));
Test("Symlink in transfer rejected", d => Unchanged(d, t => {
    File.Delete(Path.Combine(t, "1", "profile.save"));
    File.CreateSymbolicLink(Path.Combine(t, "1", "profile.save"), Path.Combine(d, "desktop", "profile.save"));
}));
Test("Changed manifest rejected", d => Unchanged(d, t => File.AppendAllText(Path.Combine(t, "manifest.json"), " ")));
Test("Newer local save wins", d => {
    string transfer = Transfer(d);
    Save(Path.Combine(d, "default", "1"), "newest-local", 0);
    SyncImportPatch.TryImport();
    Assert(File.ReadAllText(Path.Combine(d, "default", "1", "profile.save")) == "newest-local", "Stale import accepted");
});
Test("Legacy incomplete inbox left untouched", d => {
    Save(Path.Combine(d, "default", "1"), "original");
    Directory.CreateDirectory(Path.Combine(d, "sync_inbox", "9999999999", "1"));
    SyncImportPatch.TryImport();
    Assert(File.ReadAllText(Path.Combine(d, "default", "1", "profile.save")) == "original", "Empty legacy folder imported");
});
Test("Replacement keeps previous tree", d => {
    Save(Path.Combine(d, "source"), "new"); Save(Path.Combine(d, "target"), "old");
    SaveDirectoryStore.Replace(Path.Combine(d, "source"), Path.Combine(d, "target"));
    Assert(File.ReadAllText(Path.Combine(d, "target", "profile.save")) == "new", "New tree missing");
    Assert(File.ReadAllText(Path.Combine(d, "target.previous", "profile.save")) == "old", "Previous tree missing");
});
Test("Interrupted rename restores old tree before import", d => {
    Save(Path.Combine(d, "default.previous", "1"), "old");
    Save(Path.Combine(d, "default.pending", "1"), "pending");
    SaveDirectoryStore.RecoverSaves(d);
    Assert(File.ReadAllText(Path.Combine(d, "default", "1", "profile.save")) == "old", "Interrupted default not recovered");
});
Test("Interrupted profile replacement preserves sibling profiles", d => {
    Save(Path.Combine(d, "default", "1.previous"), "old");
    Save(Path.Combine(d, "default", "2"), "other");
    SaveDirectoryStore.RecoverSaves(d);
    Assert(File.ReadAllText(Path.Combine(d, "default", "1", "profile.save")) == "old", "Profile not recovered");
    Assert(File.ReadAllText(Path.Combine(d, "default", "2", "profile.save")) == "other", "Sibling lost");
});
Test("Interrupted snapshot recovers", d => {
    Save(Path.Combine(d, "snapshots", "slot_1.previous"), "old-slot");
    SaveDirectoryStore.RecoverSaves(d);
    Assert(File.ReadAllText(Path.Combine(d, "snapshots", "slot_1", "profile.save")) == "old-slot", "Slot not recovered");
});
Test("Invalid source preserves original", d => {
    Save(Path.Combine(d, "target"), "old");
    Directory.CreateDirectory(Path.Combine(d, "empty"));
    try { SaveDirectoryStore.Replace(Path.Combine(d, "empty"), Path.Combine(d, "target")); }
    catch (IOException) { }
    Assert(File.ReadAllText(Path.Combine(d, "target", "profile.save")) == "old", "Original changed");
});
Console.WriteLine($"Save safety: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
