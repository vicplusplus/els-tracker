using System.IO;

namespace ElsTracker.Services;

public record ClassItem(string Name, string IconPath);

public static class ClassCatalog
{
    private static Dictionary<string, ClassItem> _byName = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ClassItem> All { get; private set; } = Array.Empty<ClassItem>();

    public static void Load()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Classes");
        if (!Directory.Exists(dir))
        {
            All = Array.Empty<ClassItem>();
            _byName = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var items = new List<ClassItem>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.png"))
        {
            var name = ParseName(Path.GetFileNameWithoutExtension(file));
            if (string.IsNullOrWhiteSpace(name)) continue;
            items.Add(new ClassItem(name, file));
        }
        items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        All = items;
        _byName = items.ToDictionary(i => i.Name, i => i, StringComparer.OrdinalIgnoreCase);
    }

    private static string ParseName(string fileNameNoExt)
    {
        // "Icon_-_Aether_Sage" -> "Aether Sage"
        const string prefix = "Icon_-_";
        var s = fileNameNoExt;
        if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            s = s.Substring(prefix.Length);
        return s.Replace('_', ' ').Trim();
    }

    public static string? IconFor(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;
        return _byName.TryGetValue(className, out var item) ? item.IconPath : null;
    }
}
