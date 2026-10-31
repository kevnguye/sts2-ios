using System;
using System.Globalization;
using System.IO;

namespace STS2MobileIos;

// Independent of the game and UI scale. Each value is percent per side.
public sealed class ScreenMarginSettings
{
    public const int Maximum = 15;
    private readonly string _path;
    public int Horizontal { get; private set; }
    public int Vertical { get; private set; }
    public ScreenMarginSettings(string path)
    {
        _path = path;
        try
        {
            var parts = File.ReadAllText(path).Trim().Split(',');
            if (parts.Length == 3 && parts[0] == "1" &&
                int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int x) &&
                int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int y) &&
                Valid(x) && Valid(y)) { Horizontal = x; Vertical = y; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    private static bool Valid(int v) => v >= 0 && v <= Maximum;
    public void Set(int x, int y)
    {
        if (!Valid(x) || !Valid(y)) throw new ArgumentOutOfRangeException();
        string pending = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(FormattableString.Invariant($"1,{x},{y}"));
                file.Write(bytes); file.Flush(true);
            }
            File.Move(pending, _path, true);
            Horizontal = x; Vertical = y;
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
}
