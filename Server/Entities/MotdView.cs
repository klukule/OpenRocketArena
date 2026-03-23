namespace OpenRocketArena.Server.Entities;

public class MotdView
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string MotdId { get; set; } = string.Empty;

    public PlayerProfile Profile { get; set; } = null!;
}
