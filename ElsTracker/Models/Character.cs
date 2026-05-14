namespace ElsTracker.Models;

public class Character
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClassName { get; set; } = "";
    public string Ign { get; set; } = "";
    public bool Doom { get; set; }
    public bool DoomExcluded { get; set; }
    public bool Serp { get; set; }
    public bool SerpExcluded { get; set; }
    public bool Abyss { get; set; }
    public bool AbyssExcluded { get; set; }
    public bool Challenge { get; set; }
    public bool ChallengeExcluded { get; set; }
    public bool Atma { get; set; }
    public bool AtmaExcluded { get; set; }
    public bool Henir { get; set; }
    public bool HenirExcluded { get; set; }
    public string Notes { get; set; } = "";
}
