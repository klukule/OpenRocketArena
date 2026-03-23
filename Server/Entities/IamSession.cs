namespace OpenRocketArena.Server.Entities;

public class IamSession
{
    public long Id { get; set; }
    public long? AccountId { get; set; }
    public long PersonaId { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account? Account { get; set; }
}
