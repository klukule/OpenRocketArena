namespace OpenRocketArena.Server.Entities;

public class Persona
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string NamespaceName { get; set; } = "steam";
    public bool IsVisible { get; set; } = true;
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = null!;
}
