namespace OpenRocketArena.Server.Entities;

public class Account
{
    public long Id { get; set; }
    public string SteamId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    public List<Persona> Personas { get; set; } = [];
    public List<OAuthSession> Sessions { get; set; } = [];
    public List<Quest> Quests { get; set; } = [];
    public PlayerProfile? Profile { get; set; }
    public PlayerInventory? Inventory { get; set; }
    public List<IamSession> IamSessions { get; set; } = [];
    public List<MatchHistory> MatchHistories { get; set; } = [];
}
