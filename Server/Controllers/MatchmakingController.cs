using Microsoft.AspNetCore.Mvc;
using OpenRocketArena.Server.Models;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Matchmaking endpoints
/// </summary>
[ApiController]
public class MatchmakingController(IConfiguration config, MatchmakingService matchmaking, LobbyService lobby, IamTokenStore tokenStore, ILogger<MatchmakingController> logger) : ControllerBase
{
    [HttpGet("/matchmaking/qos_endpoints")]
    public IActionResult GetQosEndpoints()
    {
        var regions = config.GetSection("Qos:Regions").GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? "");

        var matchmakingRegions = config.GetSection("Qos:MatchmakingRegions").GetChildren()
            .ToDictionary(c => c.Key, c => c.GetChildren().Select(v => v.Value ?? "").ToList());

        return Ok(new
        {
            Regions = regions,
            MatchmakingRegions = matchmakingRegions
        });
    }

    [HttpPost("/matchmaking/matchmaking/start/{ticketId}")]
    public IActionResult StartMatchmaking(string ticketId, [FromBody] StartMatchmakingRequest request)
    {
        var playerId = ResolvePlayerId();

        var ticket = matchmaking.StartMatchmaking(
            ticketId, playerId, request.Playlist, request.UserSelectedMatchmakingRegion, request.RegionPings);

        return Ok(new StartMatchmakingResponse
        {
            MatchmakingTicket = matchmaking.ToDto(ticket),
            Region = request.UserSelectedMatchmakingRegion
        });
    }

    [HttpPost("/matchmaking/matchmaking/describe/{ticketId}")]
    public IActionResult DescribeMatchmaking(string ticketId)
    {
        var ticket = matchmaking.GetTicket(ticketId);
        if (ticket == null)
            return NotFound(new { error = "ticket_not_found" });

        return Ok(new DescribeMatchmakingResponse
        {
            TicketList = [matchmaking.ToDto(ticket)]
        });
    }

    [HttpPost("/matchmaking/custom_game/start/{ticketId}")]
    public IActionResult StartCustomGame(string ticketId, [FromBody] CustomGameStartRequest request)
    {
        var playerId = ResolvePlayerId();

        // Get the private match state from the player's party data
        var partyInfo = lobby.GetPlayerPartyInfo(playerId);
        string? privateMatchJson = null;

        if (partyInfo.Data != null)
        {
            try
            {
                var partyData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(partyInfo.Data);
                partyData?.TryGetValue("PrivateMatchState", out privateMatchJson);
            }
            catch { }
        }

        GameSessionData? sessionData = null;
        if (!string.IsNullOrEmpty(privateMatchJson))
        {
            try
            {
                sessionData = System.Text.Json.JsonSerializer.Deserialize<GameSessionData>(privateMatchJson);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse PrivateMatchState");
            }
        }

        sessionData ??= new GameSessionData { IsPrivateMatchSession = true };
        sessionData.IsPrivateMatchSession = true;

        // Start the server immediately - no matchmaking queue
        var ticket = matchmaking.StartCustomGame(ticketId, playerId, sessionData, request.RegionPings);

        // TODO: Probably could be simple 204 - game seems to immediately poll the describe and doesn't rely on this response other than success/failure 
        return Ok(new StartMatchmakingResponse
        {
            MatchmakingTicket = matchmaking.ToDto(ticket),
            Region = request.RegionPings.Keys.FirstOrDefault() ?? ""
        });
    }

    [HttpPost("/matchmaking/custom_game/describe/{ticketId}")]
    public IActionResult DescribeCustomGame(string ticketId)
    {
        var ticket = matchmaking.GetTicket(ticketId);
        if (ticket == null)
            return NotFound(new { error = "ticket_not_found" });

        var playerId = ResolvePlayerId();

        return Ok(new DescribeCustomServerAllocationResponse
        {
            GameSessionPlacement = new GameLiftSessionPlacementDto
            {
                GameSessionArn = ticket.GameSessionArn,
                GameSessionId = ticket.TicketId,
                GameSessionRegion = ticket.Region,
                IpAddress = ticket.IpAddress,
                Port = ticket.Port,
                MaximumPlayerSessionCount = 6,
                PlacedPlayerSessions = ticket.MatchedTickets
                    .Select(t => new PlayerSessionDto
                    {
                        PlayerId = t.PlayerId,
                        PlayerSessionId = t.PlayerSessionId
                    }).ToList(),
                PlacementId = ticket.TicketId,
                StartTime = ticket.StartTime.ToString("O"),
                Status = ticket.Status switch
                {
                    MatchmakingStatus.Completed => "FULFILLED",
                    MatchmakingStatus.Placing => "PENDING",
                    MatchmakingStatus.Failed => "FAILED",
                    MatchmakingStatus.Cancelled => "CANCELLED",
                    MatchmakingStatus.TimedOut => "TIMED_OUT",
                    _ => "PENDING"
                }
            }
        });
    }

    [HttpPost("/matchmaking/custom_game/stop/{ticketId}")]
    public IActionResult StopCustomGame(string ticketId)
    {
        matchmaking.CancelTicket(ticketId);
        return NoContent();
    }

    [HttpPost("/matchmaking/matchmaking/stop/{ticketId}")]
    public IActionResult StopMatchmaking(string ticketId)
    {
        logger.LogInformation("Matchmaking stop: ticket={TicketId}", ticketId);
        matchmaking.CancelTicket(ticketId);
        return NoContent();
    }

    private string ResolvePlayerId()
    {
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var info = tokenStore.Resolve(auth["Bearer ".Length..]);
            if (info != null) return info.PlayerId;
        }
        return "0:0:1";
    }
}
