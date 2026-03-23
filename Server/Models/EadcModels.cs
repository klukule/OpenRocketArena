using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }
}

public class TokenInfoResponse
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "JETSON_STEAM_CLIENT";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("persona_id")]
    public string? PersonaId { get; set; }

    [JsonPropertyName("pid_id")]
    public string PidId { get; set; } = string.Empty;

    [JsonPropertyName("pid_type")]
    public string PidType { get; set; } = "STEAM";

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "dp.commerce.firstpartyentitlement.w basic.citadel.write dp.commerce.firstpartyentitlement.r gos_friends_enduser dp.commerce.valuetransfer.r dp.friends.platforms.xbox offline dp.commerce.valuetransfer.w security.challenge dp.client.default signin basic.kanas basic.entitlement dp.friends.platforms.steam basic.domaindata basic.personaexternalref dp.friends.platforms.ea dp.commerce.firstpartyoffer.r basic.citadel.read dp.commerce.firstpartycheckout.r dp.progression.client.default dp.friends.platforms.psn basic.identity basic.persona dp.commerce.firstpartycheckout.w";
}

public class PersonasResponse
{
    [JsonPropertyName("personas")]
    public PersonasWrapper Personas { get; set; } = new();
}

public class PersonasWrapper
{
    [JsonPropertyName("persona")]
    public List<PersonaDto> Persona { get; set; } = [];
}

public class PersonaDto
{
    [JsonPropertyName("personaId")]
    public string PersonaId { get; set; } = string.Empty;

    [JsonPropertyName("pidId")]
    public string PidId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespaceName")]
    public string NamespaceName { get; set; } = "steam";

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = true;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ACTIVE";

    [JsonPropertyName("statusReasonCode")]
    public string StatusReasonCode { get; set; } = string.Empty;

    [JsonPropertyName("showPersona")]
    public string ShowPersona { get; set; } = "EVERYONE";

    [JsonPropertyName("dateCreated")]
    public string DateCreated { get; set; } = string.Empty;

    [JsonPropertyName("lastAuthenticated")]
    public string LastAuthenticated { get; set; } = string.Empty;

    [JsonPropertyName("authenticationSource")]
    public string AuthenticationSource { get; set; } = "AUTHENTICATOR_ANONYMOUS";

    [JsonPropertyName("nickName")]
    public string NickName { get; set; } = string.Empty;
}
