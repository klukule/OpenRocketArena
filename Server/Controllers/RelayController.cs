using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Relay endpoints
/// </summary>
[ApiController]
public class RelayController(IamTokenStore tokenStore, IConfiguration config, ILogger<RelayController> logger) : ControllerBase
{
    private string TokenIssuer => config["Vivox:TokenIssuer"] ?? "";
    private string TokenKey => config["Vivox:TokenKey"] ?? "";
    private string Domain => config["Vivox:Domain"] ?? "";

    [HttpPost("/relay/vivoxtoken")]
    public async Task<IActionResult> GetVivoxToken()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var request = JsonSerializer.Deserialize<VivoxTokenRequest>(body);

        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        // Resolve persona ID from IAM Bearer token
        var bearerToken = Request.Headers.Authorization.FirstOrDefault();
        string personaId = "0";
        if (bearerToken != null && bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = bearerToken["Bearer ".Length..];
            var info = tokenStore.Resolve(token);
            if (info != null)
                personaId = info.PersonaId.ToString();
        }

        var action = request.Action ?? "login";
        var channelName = request.ChannelName ?? "";

        var vivoxToken = action switch
        {
            "login" => GenerateLoginToken(personaId),
            "join" => GenerateJoinToken(personaId, channelName),
            _ => GenerateLoginToken(personaId)
        };

        logger.LogInformation("Vivox {Action} token issued for persona {PersonaId}", action, personaId);

        return Ok(new VivoxTokenResponse { Token = vivoxToken });
    }

    private string GenerateLoginToken(string personaId)
    {
        var exp = DateTimeOffset.UtcNow.AddSeconds(90).ToUnixTimeSeconds();
        var vxi = GenerateVxi();
        var fromUri = $"sip:.{TokenIssuer}.{personaId}.@{Domain}";

        var claims = new Dictionary<string, object>
        {
            ["iss"] = TokenIssuer,
            ["exp"] = exp,
            ["vxa"] = "login",
            ["vxi"] = vxi,
            ["f"] = fromUri
        };

        return SignToken(claims);
    }

    private string GenerateJoinToken(string personaId, string channelName)
    {
        var exp = DateTimeOffset.UtcNow.AddSeconds(90).ToUnixTimeSeconds();
        var vxi = GenerateVxi();
        var fromUri = $"sip:.{TokenIssuer}.{personaId}.@{Domain}";
        var toUri = $"sip:confctl-g-{TokenIssuer}.{channelName}@{Domain}";

        var claims = new Dictionary<string, object>
        {
            ["iss"] = TokenIssuer,
            ["exp"] = exp,
            ["vxa"] = "join",
            ["vxi"] = vxi,
            ["f"] = fromUri,
            ["t"] = toUri
        };

        return SignToken(claims);
    }

    private string SignToken(Dictionary<string, object> claims)
    {
        var header = Base64UrlEncode("{}"u8.ToArray());
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)));
        var message = $"{header}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TokenKey));
        var sig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));

        return $"{message}.{sig}";
    }

    private static string Base64UrlEncode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static long GenerateVxi() => Math.Abs(BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8)));
}

public class VivoxTokenRequest
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("channelName")]
    public string? ChannelName { get; set; }
}

public class VivoxTokenResponse
{
    public string Token { get; set; } = string.Empty;
}
