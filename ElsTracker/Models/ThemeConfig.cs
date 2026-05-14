namespace ElsTracker.Models;

public class ThemeConfig
{
    public Dictionary<string, string> Raids { get; set; } = new();
    public ChallengeRotation Challenge { get; set; } = new();
}

public class ChallengeRotation
{
    public DateTime AnchorWeekUtc { get; set; }
    public List<ChallengeEntry> Rotation { get; set; } = new();
}

public class ChallengeEntry
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}
