namespace OpenRocketArena.Server.Entities;

public class CharacterProgression
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string CharacterId { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public float Progress { get; set; }
    public string LastPlayedMatchId { get; set; } = string.Empty;

    public PlayerProfile Profile { get; set; } = null!;
    public List<EquipmentSet> EquipmentSets { get; set; } = [];
    public List<CharacterEmote> Emotes { get; set; } = [];
}

public class EquipmentSet
{
    public long Id { get; set; }
    public long CharacterProgressionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRanked { get; set; }

    public CharacterProgression CharacterProgression { get; set; } = null!;
    public List<EquipmentSetItem> Items { get; set; } = [];
}

public class EquipmentSetItem
{
    public long Id { get; set; }
    public long EquipmentSetId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;

    public EquipmentSet EquipmentSet { get; set; } = null!;
}

public class CharacterEmote
{
    public long Id { get; set; }
    public long CharacterProgressionId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Slot { get; set; }
    public EmoteType EmoteType { get; set; }

    public CharacterProgression CharacterProgression { get; set; } = null!;
}

public enum EmoteType
{
    PreGame,
    Chat,
    VictoryPose
}
