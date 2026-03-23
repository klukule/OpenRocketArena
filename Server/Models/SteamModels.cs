using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class SteamAuthTicketResponse
{
    [JsonPropertyName("response")]
    public SteamAuthTicketResponseInner Response { get; set; } = null!;
}

public class SteamAuthTicketResponseInner
{
    [JsonPropertyName("params")]
    public SteamAuthTicketParams? Params { get; set; }

    [JsonPropertyName("error")]
    public SteamError? Error { get; set; }
}

public class SteamAuthTicketParams
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("steamid")]
    public string SteamId { get; set; } = string.Empty;

    [JsonPropertyName("ownersteamid")]
    public string OwnerSteamId { get; set; } = string.Empty;

    [JsonPropertyName("vacbanned")]
    public bool VacBanned { get; set; }

    [JsonPropertyName("publisherbanned")]
    public bool PublisherBanned { get; set; }
}

public class SteamError
{
    [JsonPropertyName("errorcode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("errordesc")]
    public string ErrorDesc { get; set; } = string.Empty;
}

public class SteamPlayerSummariesResponse
{
    [JsonPropertyName("response")]
    public SteamPlayerSummariesInner Response { get; set; } = null!;
}

public class SteamPlayerSummariesInner
{
    [JsonPropertyName("players")]
    public List<SteamPlayer> Players { get; set; } = [];
}

public class SteamPlayer
{
    [JsonPropertyName("steamid")]
    public string SteamId { get; set; } = string.Empty;

    [JsonPropertyName("personaname")]
    public string PersonaName { get; set; } = string.Empty;

    [JsonPropertyName("avatarfull")]
    public string AvatarFull { get; set; } = string.Empty;
}
