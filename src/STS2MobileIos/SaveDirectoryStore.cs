using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace STS2MobileIos;

// Recoverable rename transaction, not an atomic exchange. The previous directory
// survives a crash between renames and is restored before any saves are read.
public static class SaveDirectoryStore
{
    private static readonly object Gate = new();

    public static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Links are not allowed in save paths: {path}");
    }

    public static Dictionary<string, string> Inventory(string root)
    {
        RejectLink(root);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        void Visit(string directory)
        {
            RejectLink(directory);
            foreach (string file in Directory.GetFiles(directory))
            {
                RejectLink(file);
                using var stream = File.OpenRead(file);
                result.Add(Path.GetRelativePath(root, file).Replace('\\', '/'),
                    stream.Length + ":" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
            }
            foreach (string child in Directory.GetDirectories(directory)) Visit(child);
        }
        Visit(root);
        return result;
    }

    public static bool Same(Dictionary<string, string> left, Dictionary<string, string> right) =>
        left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var v) && v == p.Value);

    public static void Recover(string target)
    {
        lock (Gate)
        {
            string previous = target + ".previous";
            if (Directory.Exists(target)) RejectLink(target);
            if (Directory.Exists(previous))
            {
                RejectLink(previous);
                if (!Directory.Exists(target)) Directory.Move(previous, target);
            }
            if (File.Exists(target) || File.Exists(previous))
                throw new IOException("A file blocks save directory recovery");
        }
    }

    public static void RecoverSaves(string root)
    {
        RejectLink(root);
        string saves = Path.Combine(root, "default");
        Recover(saves);
        if (Directory.Exists(saves)) Recover(Path.Combine(saves, "1"));
        string snapshots = Path.Combine(root, "snapshots");
        if (Directory.Exists(snapshots))
        {
            RejectLink(snapshots);
            for (int slot = 1; slot <= 3; slot++) Recover(Path.Combine(snapshots, "slot_" + slot));
        }
    }

    public static void Replace(string source, string target, Dictionary<string, string> expected = null)
    {
        lock (Gate)
        {
            Recover(target);
            RejectLink(Path.GetDirectoryName(target));
            string pending = target + ".pending", previous = target + ".previous";
            var before = Inventory(source);
            if (before.Count == 0) throw new IOException("Refusing to replace saves with an empty tree");
            if (expected != null && !Same(expected, before))
                throw new IOException("Transfer changed after manifest validation");
            if (Directory.Exists(pending))
            {
                RejectLink(pending);
                Directory.Delete(pending, true);
            }
            void Copy(string from, string to)
            {
                RejectLink(from);
                Directory.CreateDirectory(to);
                foreach (string file in Directory.GetFiles(from))
                {
                    RejectLink(file);
                    File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
                }
                foreach (string dir in Directory.GetDirectories(from))
                    Copy(dir, Path.Combine(to, Path.GetFileName(dir)));
            }
            Copy(source, pending);
            if (!Same(before, Inventory(pending)) || !Same(before, Inventory(source)))
                throw new IOException("Save changed during staging; original retained");
            // Previous is removed only while a valid current target still exists.
            if (Directory.Exists(previous)) Directory.Delete(previous, true);
            bool movedOld = false;
            try
            {
                if (Directory.Exists(target))
                {
                    Directory.Move(target, previous);
                    movedOld = true;
                }
                Directory.Move(pending, target);
            }
            catch
            {
                if (movedOld && !Directory.Exists(target)) Directory.Move(previous, target);
                throw;
            }
            // Keep .previous until the next transaction's staging has succeeded.
        }
    }
}
