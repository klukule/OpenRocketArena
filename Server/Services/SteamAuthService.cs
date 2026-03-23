using System.Text.Json;
using OpenRocketArena.Server.Models;

namespace OpenRocketArena.Server.Services;

public class SteamAuthService(HttpClient httpClient, IConfiguration config, ILogger<SteamAuthService> logger)
{
    private string ApiKey => config["Steam:ApiKey"] ?? throw new InvalidOperationException("Steam:ApiKey not configured");
    private int AppId => config.GetValue<int>("Steam:AppId");
    private string ApiHost => config["Steam:ApiHost"] ?? "partner.steam-api.com";

    /// <summary>
    /// Validates a Steam auth session ticket (hex-encoded) and returns the SteamID.
    /// Uses ISteamUserAuth/AuthenticateUserTicket.
    /// </summary>
    public async Task<string?> AuthenticateUserTicket(string ticketHex)
    {
        var url = $"https://{ApiHost}/ISteamUserAuth/AuthenticateUserTicket/v1/?key={ApiKey}&appid={AppId}&ticket={ticketHex}";

        try
        {
            var response = await httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SteamAuthTicketResponse>(json);

            if (result?.Response.Params is { } p && p.Result == "OK")
            {
                logger.LogInformation("Steam ticket validated for SteamID {SteamId}", p.SteamId);
                return p.SteamId;
            }

            logger.LogWarning("Steam ticket validation failed: {Error}", result?.Response.Error?.ErrorDesc ?? "unknown");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate Steam ticket");
            return null;
        }
    }

    /// <summary>
    /// Gets player summary (display name, etc.) for a given SteamID.
    /// </summary>
    public async Task<SteamPlayer?> GetPlayerSummary(string steamId)
    {
        var url = $"https://{ApiHost}/ISteamUser/GetPlayerSummaries/v2/?key={ApiKey}&steamids={steamId}";

        try
        {
            var response = await httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SteamPlayerSummariesResponse>(json);
            return result?.Response.Players.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get Steam player summary for {SteamId}", steamId);
            return null;
        }
    }
}
