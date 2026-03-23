using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class IamTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("bans")]
    public List<object> Bans { get; set; } = [];

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "steam";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("jflgs")]
    public int Jflgs { get; set; }

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "fsg";

    [JsonPropertyName("owned_game_version")]
    public string OwnedGameVersion { get; set; } = "mythic";

    [JsonPropertyName("permissions")]
    public List<object> Permissions { get; set; } = [];

    [JsonPropertyName("platform_additional_ids")]
    public PlatformAdditionalIds PlatformAdditionalIds { get; set; } = new();

    [JsonPropertyName("platform_id")]
    public string PlatformId { get; set; } = "steam";

    [JsonPropertyName("platform_user_id")]
    public string PlatformUserId { get; set; } = string.Empty;

    [JsonPropertyName("roles")]
    public List<object> Roles { get; set; } = [];

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("xuid")]
    public string Xuid { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class PlatformAdditionalIds
{
    [JsonPropertyName("pid_id")]
    public string PidId { get; set; } = string.Empty;
}
