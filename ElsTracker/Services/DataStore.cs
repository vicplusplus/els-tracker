using System.IO;
using System.Text.Json;
using ElsTracker.Models;

namespace ElsTracker.Services;

public static class DataStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "els-tracker",
            "data.json");

    public static AppData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppData();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
        }
        catch
        {
            return new AppData();
        }
    }

    public static void Save(AppData data)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(FilePath, json);
    }
}
