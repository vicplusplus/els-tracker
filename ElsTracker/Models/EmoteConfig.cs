namespace ElsTracker.Models;

public class EmoteConfig
{
    public Dictionary<string, string> ClassAbbreviations { get; set; } = new();
    public Dictionary<string, string> RaidEmotes { get; set; } = new();
    public Dictionary<string, string> ChallengeRotationEmotes { get; set; } = new();
}
