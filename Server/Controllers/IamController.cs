using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;
using OpenRocketArena.Server.Entities;
using OpenRocketArena.Server.Models;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango IAM service endpoints
/// </summary>
[ApiController]
public class IamController(AppDbContext db, SteamAuthService steam, IamTokenStore tokenStore, IConfiguration config, ILogger<IamController> logger) : ControllerBase
{
    private const int AccessTokenLifetimeSeconds = 3600;
    private const int RefreshTokenLifetimeDays = 30;


    [HttpPost("/iam/oauth/token")]
    public async Task<IActionResult> Token()
    {
        // Validate Basic auth (client credentials)
        var clientName = ValidateBasicAuth();
        if (clientName == null)
        {
            logger.LogWarning("IAM token request with invalid client credentials");
            return Unauthorized(new { error = "invalid_client" });
        }

        logger.LogInformation("IAM request from client {ClientName}", clientName);

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var parsed = System.Web.HttpUtility.ParseQueryString(body);

        var grantType = parsed["grant_type"] ?? "";
        var username = parsed["username"] ?? "";
        var password = parsed["password"] ?? "";

        // platform:eanucleus grant - password is the EADC access token
        if (grantType == "password" && username == "platform:eanucleus")
        {
            return await HandleEadcTokenGrant(password);
        }

        if (grantType == "refresh_token")
        {
            var refreshToken = parsed["refresh_token"] ?? "";
            return HandleRefreshGrant(refreshToken);
        }

        // Dedicated server uses client_credentials
        if (grantType == "client_credentials")
        {
            return HandleClientCredentialsGrant(clientName);
        }

        logger.LogWarning("IAM unsupported grant: grant_type={GrantType} username={Username}", grantType, username);
        return BadRequest(new { error = "unsupported_grant_type" });
    }

    private async Task<IActionResult> HandleEadcTokenGrant(string eadcAccessToken)
    {
        // Look up the EADC session by access token
        var eadcSession = await db.OAuthSessions
            .Include(s => s.Account).ThenInclude(a => a.Personas)
            .FirstOrDefaultAsync(s => s.AccessToken == eadcAccessToken && s.AccessTokenExpiresAt > DateTime.UtcNow);

        if (eadcSession == null)
        {
            logger.LogWarning("IAM token request with invalid EADC access token");
            return Unauthorized(new { error = "invalid_eadc_token" });
        }

        var account = eadcSession.Account;
        var persona = account.Personas.FirstOrDefault();

        // Also validate the Steam ticket from x-fsg-platform-authorization if present
        var platformAuth = Request.Headers["x-fsg-platform-authorization"].FirstOrDefault();
        string? steamId = null;
        if (!string.IsNullOrEmpty(platformAuth))
        {
            steamId = await steam.AuthenticateUserTicket(platformAuth);
            if (steamId != null && steamId != account.SteamId)
            {
                logger.LogWarning("IAM platform auth SteamID {TicketSteamId} doesn't match account SteamID {AccountSteamId}", steamId, account.SteamId);
                return Unauthorized(new { error = "steam_id_mismatch" });
            }
        }

        // Create IAM session tokens
        var iamAccessToken = OAuthSession.GenerateToken();
        var iamRefreshToken = OAuthSession.GenerateToken();

        var userId = account.Id.ToString();
        var personaId = persona?.Id.ToString() ?? "0";

        var fullPlayerId = $"{userId}:{personaId}:1";
        var tokenInfo = new IamTokenInfo(account.Id, persona?.Id ?? 0, fullPlayerId);
        tokenStore.Store(iamAccessToken, iamRefreshToken, tokenInfo, AccessTokenLifetimeSeconds);

        logger.LogInformation("IAM token issued for account {AccountId} (Steam {SteamId})", account.Id, account.SteamId);

        return Ok(new IamTokenResponse
        {
            AccessToken = iamAccessToken,
            ExpiresIn = AccessTokenLifetimeSeconds,
            PlatformAdditionalIds = new PlatformAdditionalIds { PidId = userId },
            PlatformUserId = account.SteamId,
            UserId = fullPlayerId,
            RefreshToken = iamRefreshToken
        });
    }

    private IActionResult HandleClientCredentialsGrant(string clientName)
    {
        var accessToken = OAuthSession.GenerateToken();
        var refreshToken = OAuthSession.GenerateToken();

        var serverPlayerId = $"server:{clientName}:0"; // TODO: Update DB to not require player id
        var tokenInfo = new IamTokenInfo(0, 0, serverPlayerId);
        tokenStore.Store(accessToken, refreshToken, tokenInfo, AccessTokenLifetimeSeconds);

        logger.LogInformation("IAM client_credentials token issued for client {ClientName}", clientName);

        return Ok(new IamTokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = AccessTokenLifetimeSeconds,
            DisplayName = clientName,
            Namespace = "fsg",
            OwnedGameVersion = "",
            PlatformId = "",
            PlatformUserId = "",
            PlatformAdditionalIds = new PlatformAdditionalIds(),
            UserId = serverPlayerId,
            RefreshToken = refreshToken
        });
    }

    private IActionResult HandleRefreshGrant(string refreshToken)
    {
        // Look up the original session info by refresh token
        var info = tokenStore.ResolveByRefresh(refreshToken);
        if (info == null)
        {
            logger.LogWarning("IAM refresh with invalid refresh token");
            return Unauthorized(new { error = "invalid_refresh_token" });
        }

        // Issue new tokens
        var newAccessToken = OAuthSession.GenerateToken();
        var newRefreshToken = OAuthSession.GenerateToken();
        tokenStore.Store(newAccessToken, newRefreshToken, info, AccessTokenLifetimeSeconds);

        logger.LogInformation("IAM token refreshed for account {AccountId}", info.AccountId);

        return Ok(new IamTokenResponse
        {
            AccessToken = newAccessToken,
            ExpiresIn = AccessTokenLifetimeSeconds,
            PlatformAdditionalIds = new PlatformAdditionalIds { PidId = info.AccountId.ToString() },
            PlatformUserId = "",
            UserId = info.PlayerId,
            RefreshToken = newRefreshToken
        });
    }

    private string? ValidateBasicAuth()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Basic ".Length..]));
            var parts = decoded.Split(':', 2);
            if (parts.Length != 2) return null;

            var clients = config.GetSection("Iam:Clients").GetChildren();
            foreach (var client in clients)
            {
                if (client["ClientId"] == parts[0] && client["ClientSecret"] == parts[1])
                    return client.Key;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
