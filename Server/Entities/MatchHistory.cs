namespace OpenRocketArena.Server.Entities;

public class MatchHistory
{
    public long Id { get; set; }
    public string MatchId { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string MatchEndJsonData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = null!;
}
