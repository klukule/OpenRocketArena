using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;
using OpenRocketArena.Server.Entities;
using OpenRocketArena.Server.Models;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Bare minimum implementation of EA's endpoints
/// </summary>
[ApiController]
public class EadcController(AppDbContext db, SteamAuthService steam, ILogger<EadcController> logger) : ControllerBase
{
    private const int AccessTokenLifetimeSeconds = 3600;
    private const int RefreshTokenLifetimeDays = 30;

    /// <summary>
    /// OAuth authorize endpoint. The game client hits this with a Steam auth ticket as the code.
    /// We validate the ticket with Steam, find-or-create the account, create an OAuth session,
    /// and redirect back with an auth code.
    /// </summary>
    [HttpGet("/connect/auth")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string redirect_uri,
        [FromQuery] string? steam_code,
        [FromQuery] string? client_id)
    {
        if (string.IsNullOrEmpty(steam_code))
        {
            logger.LogWarning("No steam_code provided in /connect/auth, returning mock code");
            return Redirect($"{redirect_uri}?code=mock_auth_code");
        }

        var steamId = await steam.AuthenticateUserTicket(steam_code);
        if (steamId == null)
        {
            logger.LogWarning("Steam ticket validation failed for /connect/auth");
            return Redirect($"{redirect_uri}?error=invalid_ticket");
        }

        var account = await FindOrCreateAccount(steamId);
        var session = CreateSession(account);
        db.OAuthSessions.Add(session);
        await db.SaveChangesAsync();

        logger.LogInformation("Auth code issued for account {AccountId} (Steam {SteamId})", account.Id, steamId);
        return Redirect($"{redirect_uri}?code={session.AuthCode}");
    }

    /// <summary>
    /// Token exchange endpoint. Exchanges an auth code or refresh token for access/refresh tokens.
    /// </summary>
    [HttpPost("/connect/token")]
    public async Task<IActionResult> Token()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var parsed = System.Web.HttpUtility.ParseQueryString(body);

        var grantType = parsed["grant_type"] ?? "";

        return grantType switch
        {
            "authorization_code" => await HandleAuthCodeGrant(parsed["code"] ?? ""),
            "refresh_token" => await HandleRefreshGrant(parsed["refresh_token"] ?? ""),
            _ => BadRequest(new { error = "unsupported_grant_type" })
        };
    }

    /// <summary>
    /// Token introspection. Returns info about the current access token.
    /// </summary>
    [HttpGet("/connect/tokeninfo")]
    public async Task<IActionResult> TokenInfo()
    {
        var accessToken = ExtractBearerToken();
        if (accessToken == null)
            return Unauthorized(new { error = "missing_token" });

        var session = await db.OAuthSessions
            .Include(s => s.Account).ThenInclude(a => a.Personas)
            .FirstOrDefaultAsync(s => s.AccessToken == accessToken && s.AccessTokenExpiresAt > DateTime.UtcNow);

        if (session == null)
            return Unauthorized(new { error = "invalid_token" });

        var persona = session.Account.Personas.FirstOrDefault();

        return Ok(new TokenInfoResponse
        {
            ExpiresIn = (int)(session.AccessTokenExpiresAt - DateTime.UtcNow).TotalSeconds,
            PersonaId = persona?.Id.ToString(),
            PidId = session.Account.Id.ToString(),
            UserId = session.Account.Id.ToString()
        });
    }

    /// <summary>
    /// Returns personas linked to an account.
    /// </summary>
    [HttpGet("/proxy/identity/pids/{userId}/personas")]
    public async Task<IActionResult> GetPersonas(long userId)
    {
        var account = await db.Accounts
            .Include(a => a.Personas)
            .FirstOrDefaultAsync(a => a.Id == userId);

        if (account == null)
            return NotFound(new { error = "account_not_found" });

        var response = new PersonasResponse
        {
            Personas = new PersonasWrapper
            {
                Persona = account.Personas.Select(p => new PersonaDto
                {
                    PersonaId = p.Id.ToString(),
                    PidId = account.Id.ToString(),
                    DisplayName = p.DisplayName,
                    Name = p.DisplayName,
                    NamespaceName = p.NamespaceName,
                    IsVisible = p.IsVisible,
                    Status = p.Status,
                    ShowPersona = "EVERYONE",
                    DateCreated = p.CreatedAt.ToString("O"),
                    NickName = p.DisplayName
                }).ToList()
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Value transfer stub - the game calls this but we don't need real logic here.
    /// </summary>
    [HttpPost("/proxy/citadel/valueTransfer")]
    public IActionResult ValueTransfer()
    {
        return Ok(new { });
    }

    [HttpGet("/proxy/firstpartyoffer/v2/offers")]
    public IActionResult GetFirstPartyOffers(
        [FromQuery] string? platform,
        [FromQuery] string? locale,
        [FromQuery] string? include)
    {
        return Ok(new FirstPartyOffersResponse());
    }

    // --- Friends stubs ---

    private static readonly object EmptyPaginatedResponse = new
    {
        entries = Array.Empty<object>(),
        pagingInfo = new { size = 0, offset = 0, totalSize = 0 }
    };

    [HttpGet("/friends/{version}/users/{userId}/block")]
    public IActionResult GetBlockList(int version, string userId) => Ok(EmptyPaginatedResponse);

    [HttpGet("/friends/{version}/personas/{personaId}/muted")]
    public IActionResult GetMutedList(int version, string personaId) => Ok(EmptyPaginatedResponse);

    [HttpGet("/friends/{version}/users/{userId}/invitations/outbound")]
    public IActionResult GetOutboundInvitations(int version, string userId) => Ok(EmptyPaginatedResponse);

    [HttpGet("/friends/{version}/users/{userId}/platforms/friends")]
    public IActionResult GetPlatformFriends(int version, string userId) => Ok(EmptyPaginatedResponse);

    [HttpGet("/friends/{version}/users/{userId}/invitations/inbound")]
    public IActionResult GetInboundInvitations(int version, string userId) => Ok(EmptyPaginatedResponse);

    // --- PIN Events ---

    [HttpPost("/pinEvents")]
    public IActionResult PinEvents()
    {
        return Ok(new
        {
            totalValidEvents = 1,
            docLink = "",
            captureRequestUUID = Guid.NewGuid().ToString(),
            receiveTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            requestHeaders = new
            {
                gameIdentifier = "1114309",
                userIdentifier = (string?)null,
                appType = (string?)null,
                appId = (string?)null,
                taxv = "1.2",
                gameEnvironment = "stage",
                gameIDType = "sellid"
            },
            isValid = true,
            totalEventsParsed = 1,
            eventErrorFlags = Array.Empty<object>(),
            sessionErrorFlags = Array.Empty<object>()
        });
    }

    // --- Private helpers ---

    private async Task<Account> FindOrCreateAccount(string steamId)
    {
        var account = await db.Accounts.Include(a => a.Personas).FirstOrDefaultAsync(a => a.SteamId == steamId);

        if (account != null)
        {
            account.LastLoginAt = DateTime.UtcNow;
            return account;
        }

        // Fetch display name from Steam
        var player = await steam.GetPlayerSummary(steamId);
        var displayName = player?.PersonaName ?? $"Player_{steamId[^6..]}";

        account = new Account
        {
            SteamId = steamId,
            Username = displayName
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(); // Get the account ID

        var persona = new Persona
        {
            AccountId = account.Id,
            DisplayName = displayName
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        account.Personas.Add(persona);
        logger.LogInformation("Created account {AccountId} with persona {PersonaId} for Steam user {SteamId} ({DisplayName})", account.Id, persona.Id, steamId, displayName);

        return account;
    }

    private static OAuthSession CreateSession(Account account)
    {
        return new OAuthSession
        {
            AccountId = account.Id,
            AuthCode = OAuthSession.GenerateToken(),
            AccessToken = OAuthSession.GenerateToken(),
            RefreshToken = OAuthSession.GenerateToken(),
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(AccessTokenLifetimeSeconds),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays)
        };
    }

    private async Task<IActionResult> HandleAuthCodeGrant(string authCode)
    {
        if (string.IsNullOrEmpty(authCode))
            return BadRequest(new { error = "missing_code" });

        var session = await db.OAuthSessions.FirstOrDefaultAsync(s => s.AuthCode == authCode && !s.IsConsumed);

        if (session == null)
            return BadRequest(new { error = "invalid_code" });

        session.IsConsumed = true;
        await db.SaveChangesAsync();

        return Ok(new TokenResponse
        {
            AccessToken = session.AccessToken,
            ExpiresIn = AccessTokenLifetimeSeconds,
            RefreshToken = session.RefreshToken
        });
    }

    private async Task<IActionResult> HandleRefreshGrant(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return BadRequest(new { error = "missing_refresh_token" });

        var session = await db.OAuthSessions.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && s.RefreshTokenExpiresAt > DateTime.UtcNow);

        if (session == null)
            return BadRequest(new { error = "invalid_refresh_token" });

        // Rotate tokens
        session.AccessToken = OAuthSession.GenerateToken();
        session.RefreshToken = OAuthSession.GenerateToken();
        session.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(AccessTokenLifetimeSeconds);
        session.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays);
        await db.SaveChangesAsync();

        return Ok(new TokenResponse
        {
            AccessToken = session.AccessToken,
            ExpiresIn = AccessTokenLifetimeSeconds,
            RefreshToken = session.RefreshToken
        });
    }

    private string? ExtractBearerToken()
    {
        // Game sends token as "access_token" header
        var token = Request.Headers["access_token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(token))
            return token;

        // Fallback to standard Authorization: Bearer
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..];

        return null;
    }
}
