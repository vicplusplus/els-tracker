namespace ElsTracker.Models;

public class AppData
{
    public List<Character> Characters { get; set; } = new();
    public DateTime LastResetUtc { get; set; } = DateTime.MinValue;
}
