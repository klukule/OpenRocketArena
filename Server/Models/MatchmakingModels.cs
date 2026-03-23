using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class MatchmakingRequest
{
    [JsonPropertyName("Region")]
    public string Region { get; set; } = string.Empty;
}

public class StartMatchmakingRequest
{
    [JsonPropertyName("RegionPings")]
    public Dictionary<string, int> RegionPings { get; set; } = new();

    [JsonPropertyName("BuildBranch")]
    public string BuildBranch { get; set; } = string.Empty;

    [JsonPropertyName("BuildConfig")]
    public string BuildConfig { get; set; } = string.Empty;

    [JsonPropertyName("BuildVersion")]
    public string BuildVersion { get; set; } = string.Empty;

    [JsonPropertyName("TicketId")]
    public string TicketId { get; set; } = string.Empty;

    [JsonPropertyName("Playlist")]
    public string Playlist { get; set; } = string.Empty;

    [JsonPropertyName("CrossplayEnabled")]
    public bool CrossplayEnabled { get; set; }

    [JsonPropertyName("CrossplayState")]
    public string CrossplayState { get; set; } = string.Empty;

    [JsonPropertyName("MatchmakeFilters")]
    public List<MapFilterDto> MatchmakeFilters { get; set; } = [];

    [JsonPropertyName("CustomMatchmakingKey")]
    public string CustomMatchmakingKey { get; set; } = string.Empty;

    [JsonPropertyName("BlockedMangoIds")]
    public List<string> BlockedMangoIds { get; set; } = [];

    [JsonPropertyName("UserSelectedMatchmakingRegion")]
    public string UserSelectedMatchmakingRegion { get; set; } = string.Empty;
}

public class MapFilterDto
{
    [JsonPropertyName("MapId")]
    public string MapId { get; set; } = string.Empty;

    [JsonPropertyName("FilteredModes")]
    public List<string> FilteredModes { get; set; } = [];
}

public class StartMatchmakingResponse
{
    [JsonPropertyName("MatchmakingTicket")]
    public MatchmakingTicketDto MatchmakingTicket { get; set; } = new();

    [JsonPropertyName("Region")]
    public string Region { get; set; } = string.Empty;
}

public class MatchmakingTicketDto
{
    [JsonPropertyName("ConfigurationName")]
    public string ConfigurationName { get; set; } = string.Empty;

    [JsonPropertyName("EndTime")]
    public string EndTime { get; set; } = string.Empty;

    [JsonPropertyName("GameSessionConnectionInfo")]
    public GameSessionConnectionInfoDto GameSessionConnectionInfo { get; set; } = new();

    [JsonPropertyName("Players")]
    public List<MatchmakePlayerDto> Players { get; set; } = [];

    [JsonPropertyName("StartTime")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("StatusMessage")]
    public string StatusMessage { get; set; } = string.Empty;

    [JsonPropertyName("StatusReason")]
    public string StatusReason { get; set; } = string.Empty;

    [JsonPropertyName("TicketId")]
    public string TicketId { get; set; } = string.Empty;
}

public class GameSessionConnectionInfoDto
{
    [JsonPropertyName("GameSessionArn")]
    public string GameSessionArn { get; set; } = string.Empty;

    [JsonPropertyName("IpAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("MatchedPlayerSessions")]
    public List<PlayerSessionDto> MatchedPlayerSessions { get; set; } = [];

    [JsonPropertyName("Port")]
    public int Port { get; set; }
}

public class MatchmakePlayerDto
{
    [JsonPropertyName("LatencyInMs")]
    public int LatencyInMs { get; set; }

    [JsonPropertyName("PlayerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("Team")]
    public string Team { get; set; } = string.Empty;
}

public class PlayerSessionDto
{
    [JsonPropertyName("PlayerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("PlayerSessionId")]
    public string PlayerSessionId { get; set; } = string.Empty;
}

public class CustomGameStartRequest
{
    [JsonPropertyName("RegionPings")]
    public Dictionary<string, int> RegionPings { get; set; } = new();

    [JsonPropertyName("BuildBranch")]
    public string BuildBranch { get; set; } = string.Empty;

    [JsonPropertyName("BuildConfig")]
    public string BuildConfig { get; set; } = string.Empty;

    [JsonPropertyName("BuildVersion")]
    public string BuildVersion { get; set; } = string.Empty;
}

public class DescribeMatchmakingResponse
{
    [JsonPropertyName("TicketList")]
    public List<MatchmakingTicketDto> TicketList { get; set; } = [];
}
