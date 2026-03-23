using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class ProfileResponse
{
    public ProfileDto Profile { get; set; } = new();
}

public class ProfileDto
{
    public string MangoId { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int ArtifactUnlockLevel { get; set; }
    public float ArtifactUnlockProgress { get; set; }
    public int ArtifactUnlockXp { get; set; }
    public int CareerLevel { get; set; }
    public int CareerXp { get; set; }
    public float Progress { get; set; }
    public string LastPlayedMatchID { get; set; } = string.Empty;
    public string ActiveMatchID { get; set; } = string.Empty;
    public string ActiveMatchIDUpdatedAt { get; set; } = "0001-01-01T00:00:00Z";
    public List<CharacterProgressionDto> CharacterProgression { get; set; } = [];
    public List<EquipItemDto> Equipment { get; set; } = [];
    public List<ItemLevelDto> ItemLevels { get; set; } = [];
    public int OnboardingState { get; set; } = -1;
    public bool AdvertState { get; set; }

    [JsonPropertyName("promos_owned")]
    public string PromosOwned { get; set; } = string.Empty;

    public int BanLevel { get; set; }
    public string UnbanTime { get; set; } = "0001-01-01T00:00:00+00:00";
    public bool BanMock { get; set; }
    public List<PlaylistRankDto> PlaylistRankings { get; set; } = [];
    public List<BlastPassProgressionDto> BlastPassLevels { get; set; } = [];
    public Dictionary<string, float> Ranks { get; set; } = new();
    public List<string> MotdViews { get; set; } = [];
    public StatsDto Stats { get; set; } = new();
}

public class CharacterProgressionDto
{
    public string MangoId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int Level { get; set; }
    public int Experience { get; set; }
    public float Progress { get; set; }
    public string LastPlayedMatchId { get; set; } = string.Empty;
    public List<EquipmentSetDto> EquipmentSets { get; set; } = [];
    public List<EmoteWithSlotDto>? PreGameEmotes { get; set; }
    public List<EmoteWithSlotDto>? ChatEmotes { get; set; }
    public EmoteDto? VictoryPose { get; set; }
}

public class EquipmentSetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public bool IsRanked { get; set; }
    public List<EquipItemDto> Equipment { get; set; } = [];
}

public class EquipItemDto
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
}

public class EmoteWithSlotDto
{
    public string ItemId { get; set; } = string.Empty;
    public int Slot { get; set; }
}

public class EmoteDto
{
    public string ItemId { get; set; } = string.Empty;
}

public class ItemLevelDto
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("total_xp")]
    public float TotalXp { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }
}

public class PlaylistRankDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public float SkillMean { get; set; }
    public float SkillStdDev { get; set; }
    public float Rank { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesQuit { get; set; }
    public int Streak { get; set; }
    public int BotLevel { get; set; }
    public string CreatedAt { get; set; } = "0001-01-01T00:00:00Z";
    public string UpdatedAt { get; set; } = "0001-01-01T00:00:00Z";
}

public class BlastPassProgressionDto
{
    [JsonPropertyName("blast_pass_id")]
    public string BlastPassId { get; set; } = string.Empty;

    [JsonPropertyName("blast_pass_xp")]
    public int BlastPassXp { get; set; }

    [JsonPropertyName("blast_pass_level")]
    public int BlastPassLevel { get; set; }

    [JsonPropertyName("bp_progress")]
    public float BpProgress { get; set; }

    [JsonPropertyName("xp_bonus")]
    public int XpBonus { get; set; }

    [JsonPropertyName("party_xp_bonus")]
    public int PartyXpBonus { get; set; }

    [JsonPropertyName("viewed")]
    public bool Viewed { get; set; }
}

public class StatsDto
{
    public StatsGroupDto Lifetime { get; set; } = new();
    public SlicedStatsDto SlicedStats { get; set; } = new();
}

public class StatsGroupDto
{
    public List<StatEntryDto> Stats { get; set; } = [];
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesQuit { get; set; }
    public int GamesDrawn { get; set; }
    public string PlayerID { get; set; } = string.Empty;
    public string SliceType { get; set; } = string.Empty;
    public string SliceValue { get; set; } = string.Empty;
}

public class StatEntryDto
{
    public string Metric { get; set; } = string.Empty;
    public float Min { get; set; }
    public float Max { get; set; }
    public float Mean { get; set; }
    public float Sum { get; set; }
}

public class SlicedStatsDto
{
    [JsonPropertyName("blastpass")]
    public Dictionary<string, StatsGroupDto> Blastpass { get; set; } = new();

    [JsonPropertyName("character")]
    public Dictionary<string, StatsGroupDto> Character { get; set; } = new();

    [JsonPropertyName("map")]
    public Dictionary<string, StatsGroupDto> Map { get; set; } = new();

    [JsonPropertyName("mode")]
    public Dictionary<string, StatsGroupDto> Mode { get; set; } = new();

    [JsonPropertyName("playlist")]
    public Dictionary<string, StatsGroupDto> Playlist { get; set; } = new();
}

// --- Equip request models ---

public class EquipPayload
{
    [JsonPropertyName("characterEquip")]
    public List<EquipCharacterRequest> CharacterEquip { get; set; } = [];

    [JsonPropertyName("playerEquip")]
    public List<EquipItemRequest> PlayerEquip { get; set; } = [];
}

public class EquipCharacterRequest
{
    [JsonPropertyName("characterId")]
    public string CharacterId { get; set; } = string.Empty;

    [JsonPropertyName("equipmentSets")]
    public List<EquipmentSetRequest> EquipmentSets { get; set; } = [];

    [JsonPropertyName("chatEmotes")]
    public List<EmoteWithSlotRequest>? ChatEmotes { get; set; }

    [JsonPropertyName("preGameEmotes")]
    public List<EmoteWithSlotRequest>? PreGameEmotes { get; set; }

    [JsonPropertyName("victoryPose")]
    public EmoteRequest? VictoryPose { get; set; }
}

public class EquipmentSetRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isRanked")]
    public bool IsRanked { get; set; }

    [JsonPropertyName("equipment")]
    public List<EquipItemRequest> Equipment { get; set; } = [];
}

public class EquipItemRequest
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("equipSlot")]
    public int EquipSlot { get; set; }

    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;
}

public class EmoteWithSlotRequest
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("slot")]
    public int Slot { get; set; }
}

public class EmoteRequest
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;
}
