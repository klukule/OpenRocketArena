using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class MatchData
{
    [JsonPropertyName("matchId")]
    public string MatchId { get; set; } = string.Empty;

    [JsonPropertyName("playlistUniqueId")]
    public string PlaylistUniqueId { get; set; } = string.Empty;

    [JsonPropertyName("players")]
    public List<MatchEndPlayerDto> Players { get; set; } = [];

    [JsonPropertyName("teams")]
    public List<MatchEndTeamDto> Teams { get; set; } = [];

    [JsonPropertyName("matchTime")]
    public int MatchTime { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("bIsMatchmadeSession")]
    public bool IsMatchmadeSession { get; set; }

    [JsonPropertyName("bIsRankedSession")]
    public bool IsRankedSession { get; set; }
}

public class MatchEndPlayerDto
{
    [JsonPropertyName("mangoId")]
    public string MangoId { get; set; } = string.Empty;

    [JsonPropertyName("platformId")]
    public string PlatformId { get; set; } = string.Empty;

    [JsonPropertyName("bIsSharingUsageData")]
    public bool IsSharingUsageData { get; set; }

    [JsonPropertyName("bIsSharingExternalUsageData")]
    public bool IsSharingExternalUsageData { get; set; }

    [JsonPropertyName("bIsUnderage")]
    public bool IsUnderage { get; set; }

    [JsonPropertyName("partyId")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Dictionary<string, int> Stats { get; set; } = new();

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = string.Empty;

    [JsonPropertyName("characterId")]
    public string CharacterId { get; set; } = string.Empty;

    [JsonPropertyName("characterLevelStart")]
    public int CharacterLevelStart { get; set; }

    [JsonPropertyName("characterLevelEnd")]
    public int CharacterLevelEnd { get; set; }

    [JsonPropertyName("skinId")]
    public string SkinId { get; set; } = string.Empty;

    [JsonPropertyName("equippedArtifacts")]
    public List<MatchEndArtifactDto> EquippedArtifacts { get; set; } = [];

    [JsonPropertyName("teamIndex")]
    public int TeamIndex { get; set; }

    [JsonPropertyName("gameOutcome")]
    public string GameOutcome { get; set; } = string.Empty;

    [JsonPropertyName("rejoinCount")]
    public int RejoinCount { get; set; }

    [JsonPropertyName("bIsABot")]
    public bool IsABot { get; set; }
}

public class MatchEndArtifactDto
{
    [JsonPropertyName("artifactType")]
    public string ArtifactType { get; set; } = string.Empty;

    [JsonPropertyName("artifactId")]
    public string ArtifactId { get; set; } = string.Empty;
}

public class MatchEndTeamDto
{
    [JsonPropertyName("teamIndex")]
    public int TeamIndex { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public class GetMatchHistoryResponse
{
    [JsonPropertyName("match_history")]
    public MatchResultDto MatchHistory { get; set; } = new();
}

public class MatchResultDto
{
    [JsonPropertyName("MatchId")]
    public string MatchId { get; set; } = string.Empty;

    [JsonPropertyName("MatchEndJsonData")]
    public string MatchEndJsonData { get; set; } = string.Empty;
}
