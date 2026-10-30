using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;

namespace STS2MobileIos.Patches;

// A transfer is eligible only after its manifest and completion marker arrive.
// Validate every file before replacing profile 1; never replace other profiles.
public static class SyncImportPatch
{
    public static long ValidateTransfer(string directory)
        => ValidateTransfer(directory, out _);

    private static long ValidateTransfer(string directory,
        out System.Collections.Generic.Dictionary<string, string> expected)
    {
        SaveDirectoryStore.RejectLink(directory);
        string manifestPath = Path.Combine(directory, "manifest.json");
        string completePath = Path.Combine(directory, "complete");
        SaveDirectoryStore.RejectLink(manifestPath);
        SaveDirectoryStore.RejectLink(completePath);
        if (new FileInfo(manifestPath).Length > 8 * 1024 * 1024)
            throw new IOException("Manifest too large");
        byte[] bytes = File.ReadAllBytes(manifestPath);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (new FileInfo(completePath).Length > 128 || File.ReadAllText(completePath).Trim() != hash)
            throw new IOException("Incomplete transfer");
        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        long timestamp = root.GetProperty("timestamp").GetInt64();
        if (root.GetProperty("version").GetInt32() != 1 || timestamp <= 0 ||
            timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 300)
            throw new IOException("Invalid transfer version or timestamp");
        expected = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        long newest = 0;
        foreach (var file in root.GetProperty("files").EnumerateArray())
        {
            string path = file.GetProperty("path").GetString();
            if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p == "" || p == "." || p == ".."))
                throw new IOException("Unsafe manifest path");
            long size = file.GetProperty("size").GetInt64();
            string digest = file.GetProperty("sha256").GetString();
            if (size < 0 || digest == null || digest.Length != 64 ||
                digest.Any(c => !"0123456789abcdef".Contains(c)))
                throw new IOException("Invalid manifest entry");
            expected.Add(path, size + ":" + digest);
            if (!Path.GetFileName(path).StartsWith("sync_", StringComparison.Ordinal))
                newest = Math.Max(newest, file.GetProperty("mtime").GetInt64());
        }
        if (timestamp != newest || !expected.TryGetValue("profile.save", out var profile) ||
            profile.StartsWith("0:", StringComparison.Ordinal) ||
            !SaveDirectoryStore.Same(expected, SaveDirectoryStore.Inventory(Path.Combine(directory, "1"))))
            throw new IOException("Incomplete or modified save tree");
        return timestamp;
    }

    public static void TryImport()
    {
        try
        {
            string root = OS.GetUserDataDir();
            SaveDirectoryStore.RecoverSaves(root);
            string inbox = Path.Combine(root, "sync_inbox");
            if (!Directory.Exists(inbox)) return;
            SaveDirectoryStore.RejectLink(inbox);
            string best = null;
            long timestamp = 0;
            foreach (string candidate in Directory.GetDirectories(inbox))
            {
                try
                {
                    long value = ValidateTransfer(candidate);
                    if (value > timestamp) { best = candidate; timestamp = value; }
                }
                catch (Exception e) { Log($"Transfer left untouched: {e.Message}"); }
            }
            if (best == null) return; // Legacy/in-progress transfers are not deleted.
            string defaultDir = Path.Combine(root, "default");
            Directory.CreateDirectory(defaultDir);
            SaveDirectoryStore.RejectLink(defaultDir);
            string target = Path.Combine(defaultDir, "1");
            long localNewest = NewestMtimeUnix(target);
            if (timestamp <= localNewest) { Log("Stale transfer left untouched"); return; }
            ValidateTransfer(best, out var expected);
            SaveDirectoryStore.Replace(Path.Combine(best, "1"), target, expected);
            Directory.Delete(best, true);
            Log("Imported validated profile 1; previous profile retained in default/1.previous");
        }
        catch (Exception e) { Log($"Import failed: {e.Message}"); }
    }

    private static long NewestMtimeUnix(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        var files = SaveDirectoryStore.Inventory(directory);
        return files.Keys.Where(p => !Path.GetFileName(p).StartsWith("sync_", StringComparison.Ordinal))
            .Select(p => new DateTimeOffset(File.GetLastWriteTimeUtc(Path.Combine(directory, p))).ToUnixTimeSeconds())
            .DefaultIfEmpty(0).Max();
    }

    private static void Log(string message)
    {
        PatchHelper.Log($"[SyncImport] {message}");
        try { File.AppendAllText(Path.Combine(OS.GetUserDataDir(), "sync_log.txt"), message + "\n"); }
        catch { }
    }
}
