namespace OpenRocketArena.Server.Entities;

public class ItemLevel
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Level { get; set; }
    public float TotalXp { get; set; }
    public float Progress { get; set; }

    public PlayerProfile Profile { get; set; } = null!;
}
