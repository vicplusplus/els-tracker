using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using ElsTracker.Models;

namespace ElsTracker.Services;

public static class EmoteService
{
    private static FileSystemWatcher? _watcher;
    private static DispatcherTimer? _debounce;

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string FilePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "els-tracker",
            "emotes.json");

    public static EmoteConfig Config { get; private set; } = Default();

    public static event Action? Updated;

    public static void Load()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        if (!File.Exists(FilePath))
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Default(), WriteOpts));

        try
        {
            var json = File.ReadAllText(FilePath);
            Config = JsonSerializer.Deserialize<EmoteConfig>(json, ReadOpts) ?? Default();
        }
        catch
        {
            Config = Default();
        }

        Normalize(Config);

        StartWatcher();
        Updated?.Invoke();
    }

    private static void Normalize(EmoteConfig cfg)
    {
        cfg.ClassAbbreviations = new Dictionary<string, string>(
            cfg.ClassAbbreviations, StringComparer.OrdinalIgnoreCase);
        cfg.RaidEmotes = new Dictionary<string, string>(
            cfg.RaidEmotes, StringComparer.OrdinalIgnoreCase);
        cfg.ChallengeRotationEmotes = new Dictionary<string, string>(
            cfg.ChallengeRotationEmotes, StringComparer.OrdinalIgnoreCase);
    }

    public static string? AbbrevFor(string className)
    {
        if (string.IsNullOrEmpty(className)) return null;
        return Config.ClassAbbreviations.TryGetValue(className, out var v) ? v : null;
    }

    public static string? RaidEmoteFor(string raid)
    {
        if (string.IsNullOrEmpty(raid)) return null;
        return Config.RaidEmotes.TryGetValue(raid, out var v) ? v : null;
    }

    public static string? ChallengeEmoteFor(string rotationName)
    {
        if (string.IsNullOrEmpty(rotationName)) return null;
        return Config.ChallengeRotationEmotes.TryGetValue(rotationName, out var v) ? v : null;
    }

    private static EmoteConfig Default() => new()
    {
        ClassAbbreviations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Achlys"]          = "AC",
            ["Adrestia"]        = "AD",
            ["Aether Sage"]     = "AeS",
            ["Anemos"]          = "AN",
            ["Apsara"]          = "Aps",
            ["Avarice"]         = "AV",
            ["Black Massacre"]  = "BlM",
            ["Bloody Queen"]    = "BQ",
            ["Bluhen"]          = "BL",
            ["Catastrophe"]     = "CaT",
            ["Celestia"]        = "CL",
            ["Centurion"]       = "CeT",
            ["Code Antithese"]  = "CA",
            ["Code Esencia"]    = "CE",
            ["Code Sariel"]     = "CS",
            ["Code Ultimate"]   = "CU",
            ["Comet Crusader"]  = "CC",
            ["Daybreaker"]      = "DaB",
            ["Demersio"]        = "DeM",
            ["Devi"]            = "Devi",
            ["Diangelion"]      = "DiA",
            ["Dius Aer"]        = "DA",
            ["Dominator"]       = "DoM",
            ["Doom Bringer"]    = "DB",
            ["Empire Sword"]    = "ES",
            ["Eternity Winner"] = "EtW",
            ["Fatal Phantom"]   = "FP",
            ["Flame Lord"]      = "FL",
            ["Furious Blade"]   = "FB",
            ["Gembliss"]        = "GB",
            ["Genesis"]         = "GS",
            ["Herrscher"]       = "HR",
            ["Immortal"]        = "IM",
            ["Innocent"]        = "IN",
            ["Knight Emperor"]  = "KE",
            ["Liberator"]       = "LB",
            ["Lord Azoth"]      = "LA",
            ["Mad Paradox"]     = "MP",
            ["Metamorphy"]      = "MtM",
            ["Minerva"]         = "MN",
            ["Mischief"]        = "MC",
            ["Morpheus"]        = "MO",
            ["Nisha Labyrinth"] = "NL",
            ["Nova Imperator"]  = "NI",
            ["Nyx Pieta"]       = "NP",
            ["Opferung"]        = "OP",
            ["Overmind"]        = "OM",
            ["Oz Sorcerer"]     = "OzS",
            ["Prime Operator"]  = "PO",
            ["Prophetess"]      = "PR",
            ["Radiant Soul"]    = "RaS",
            ["Rage Hearts"]     = "RH",
            ["Revenant"]        = "RV",
            ["Richter"]         = "RT",
            ["Rune Master"]     = "RM",
            ["Shakti"]          = "SH",
            ["Surya"]           = "SU",
            ["Tempest Burster"] = "TB",
            ["Twilight"]        = "TW",
            ["Twins Picaro"]    = "TP",
        },
        RaidEmotes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Doom"]      = "DoomFresh",
            ["Serp"]      = "SerpFresh",
            ["Abyss"]     = "AbyssFresh",
        },
        ChallengeRotationEmotes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Rosso"]  = "RossoFresh",
            ["Berthe"] = "BertheFresh",
        },
    };

    private static void StartWatcher()
    {
        if (_watcher != null) return;
        var dir = Path.GetDirectoryName(FilePath)!;
        _watcher = new FileSystemWatcher(dir, "emotes.json")
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
            var cfg = JsonSerializer.Deserialize<EmoteConfig>(json, ReadOpts);
            if (cfg != null)
            {
                Config = cfg;
                Normalize(Config);
            }
            Updated?.Invoke();
        }
        catch { /* ignore malformed in-flight edits */ }
    }
}
