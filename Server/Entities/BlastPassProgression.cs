namespace OpenRocketArena.Server.Entities;

public class BlastPassProgression
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string BlastPassId { get; set; } = string.Empty;
    public int BlastPassXp { get; set; }
    public int BlastPassLevel { get; set; }
    public float BpProgress { get; set; }
    public int XpBonus { get; set; }
    public int PartyXpBonus { get; set; }
    public bool Viewed { get; set; }

    public PlayerProfile Profile { get; set; } = null!;
}
