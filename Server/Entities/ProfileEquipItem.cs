namespace OpenRocketArena.Server.Entities;

public class ProfileEquipItem
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;

    public PlayerProfile Profile { get; set; } = null!;
}
