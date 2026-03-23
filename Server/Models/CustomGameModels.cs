using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class DescribeCustomServerAllocationResponse
{
    [JsonPropertyName("GameSessionPlacement")]
    public GameLiftSessionPlacementDto GameSessionPlacement { get; set; } = new();
}

public class GameLiftSessionPlacementDto
{
    [JsonPropertyName("GameProperties")]
    public List<string> GameProperties { get; set; } = [];

    [JsonPropertyName("GameSessionArn")]
    public string GameSessionArn { get; set; } = string.Empty;

    [JsonPropertyName("GameSessionData")]
    public string GameSessionData { get; set; } = string.Empty;

    [JsonPropertyName("GameSessionId")]
    public string GameSessionId { get; set; } = string.Empty;

    [JsonPropertyName("GameSessionName")]
    public string GameSessionName { get; set; } = string.Empty;

    [JsonPropertyName("GameSessionRegion")]
    public string GameSessionRegion { get; set; } = string.Empty;

    [JsonPropertyName("IpAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("GameSessionQueueName")]
    public string GameSessionQueueName { get; set; } = string.Empty;

    [JsonPropertyName("MaximumPlayerSessionCount")]
    public int MaximumPlayerSessionCount { get; set; }

    [JsonPropertyName("PlacedPlayerSessions")]
    public List<PlayerSessionDto> PlacedPlayerSessions { get; set; } = [];

    [JsonPropertyName("PlacementId")]
    public string PlacementId { get; set; } = string.Empty;

    [JsonPropertyName("Port")]
    public int Port { get; set; }

    [JsonPropertyName("StartTime")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;
}
