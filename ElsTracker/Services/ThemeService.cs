using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ElsTracker.Models;

namespace ElsTracker.Services;

public static class ThemeService
{
    private static FileSystemWatcher? _watcher;
    private static DispatcherTimer? _debounce;

    public static string FilePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "els-tracker",
            "theme.json");

    public static ThemeConfig Config { get; private set; } = Default();
    public static string CurrentChallengeName { get; private set; } = "";

    public static event Action? Updated;

    public static void Load()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        if (!File.Exists(FilePath))
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Default(),
                new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var json = File.ReadAllText(FilePath);
            Config = JsonSerializer.Deserialize<ThemeConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? Default();
        }
        catch
        {
            Config = Default();
        }
        Apply();
        StartWatcher();
    }

    public static void Apply()
    {
        var res = Application.Current.Resources;
        foreach (var (key, hex) in Config.Raids)
            res[key + "Brush"] = ToBrush(hex);

        if (Config.Challenge.Rotation.Count > 0)
        {
            var idx = ComputeChallengeIndex(DateTime.UtcNow);
            var entry = Config.Challenge.Rotation[idx];
            res["ChallengeBrush"] = ToBrush(entry.Color);
            CurrentChallengeName = entry.Name;
        }
        Updated?.Invoke();
    }

    public static void Refresh() => Apply();

    private static int ComputeChallengeIndex(DateTime nowUtc)
    {
        var anchor = DateTime.SpecifyKind(Config.Challenge.AnchorWeekUtc.Date, DateTimeKind.Utc);
        var thisWed = ResetSchedule.LastBoundaryUtc(nowUtc);
        var weeks = (int)Math.Floor((thisWed - anchor).TotalDays / 7.0);
        var rot = Config.Challenge.Rotation.Count;
        return ((weeks % rot) + rot) % rot;
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
        catch { return Brushes.Gray; }
    }

    private static ThemeConfig Default() => new()
    {
        Raids = new()
        {
            ["Doom"]  = "#1D4ED8",
            ["Serp"]  = "#84CC16",
            ["Abyss"] = "#9333EA",
            ["Atma"]  = "#F97316",
            ["Henir"] = "#0F766E",
        },
        Challenge = new()
        {
            AnchorWeekUtc = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc),
            Rotation = new()
            {
                new() { Name = "Rosso",  Color = "#DC2626" },
                new() { Name = "Berthe", Color = "#EAB308" },
            },
        },
    };

    private static void StartWatcher()
    {
        if (_watcher != null) return;
        var dir = Path.GetDirectoryName(FilePath)!;
        _watcher = new FileSystemWatcher(dir, "theme.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _debounce.Stop();
            _debounce.Tick -= DebounceTick;
            _debounce.Tick += DebounceTick;
            _debounce.Start();
        }));
    }

    private static void DebounceTick(object? s, EventArgs e)
    {
        _debounce!.Stop();
        _debounce.Tick -= DebounceTick;
        try
        {
            var json = File.ReadAllText(FilePath);
            var cfg = JsonSerializer.Deserialize<ThemeConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cfg != null) Config = cfg;
            Apply();
        }
        catch { /* ignore malformed edits in flight */ }
    }
}
