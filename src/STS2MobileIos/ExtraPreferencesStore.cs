using System;
using System.IO;
using System.Text.Json;

namespace STS2MobileIos;

public enum ExtraFeature { SaveSlots, CardSuggestions, RestartRoom }

// Separate from game saves so imports/snapshot restores cannot re-enable extras.
// Missing, corrupt or unsupported settings fail closed. Commit to memory only
// after the complete replacement file has been written successfully.
public sealed class ExtraPreferencesStore
{
    private readonly string _path;
    private bool[] _enabled = new bool[3];
    private static readonly string[] Keys = { "save_slots", "card_suggestions", "restart_room" };

    public ExtraPreferencesStore(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = json.RootElement;
            if (root.GetProperty("version").GetInt32() != 1) return;
            var values = new bool[3];
            for (int i = 0; i < Keys.Length; i++)
                if (root.TryGetProperty(Keys[i], out var value)) values[i] = value.GetBoolean();
            _enabled = values;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or KeyNotFoundException or FormatException) { }
    }

    public bool IsEnabled(ExtraFeature feature) => _enabled[(int)feature];

    public void Set(ExtraFeature feature, bool enabled)
    {
        var next = (bool[])_enabled.Clone();
        next[(int)feature] = enabled;
        string pending = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write))
            {
                using (var writer = new Utf8JsonWriter(file))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("version", 1);
                    for (int i = 0; i < Keys.Length; i++) writer.WriteBoolean(Keys[i], next[i]);
                    writer.WriteEndObject();
                }
                file.Flush(true);
            }
            File.Move(pending, _path, true);
            _enabled = next;
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
}
