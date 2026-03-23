namespace OpenRocketArena.Server.Entities;

public class PlayerStatGroup
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string SliceType { get; set; } = string.Empty; // "lifetime", "character", "map", "mode", "playlist", "blastpass"
    public string SliceValue { get; set; } = string.Empty; // e.g. "Gant_C", guid, etc.
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesQuit { get; set; }
    public int GamesDrawn { get; set; }

    public PlayerProfile Profile { get; set; } = null!;
    public List<PlayerStatEntry> Stats { get; set; } = [];
}

public class PlayerStatEntry
{
    public long Id { get; set; }
    public long StatGroupId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public float Min { get; set; }
    public float Max { get; set; }
    public float Sum { get; set; }
    public int Count { get; set; } // to compute Mean = Sum / Count

    public PlayerStatGroup StatGroup { get; set; } = null!;
}
