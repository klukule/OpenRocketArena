using Microsoft.AspNetCore.Mvc;
using OpenRocketArena.Server.Models;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Lobby endpoints
/// </summary>
[ApiController]
public class LobbyController(LobbyService lobby) : ControllerBase
{
    [HttpGet("/lobby/presence/bulk")]
    public IActionResult GetPresenceBulk([FromQuery] string playerIds)
    {
        // Game joins player IDs with '+' separator - need to properly test out, hard to test solo
        var ids = playerIds.Split([',', '+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var presences = new Dictionary<string, Dictionary<string, string>>();

        foreach (var pid in ids)
        {
            var userData = lobby.GetPlayerUserData(pid);
            if (userData != null)
                presences[pid] = userData;
            else
                presences[pid] = new Dictionary<string, string>();
        }

        return Ok(new LobbyGetPlayerPresenceBulkResponse
        {
            PlayerPresences = presences
        });
    }

    [HttpGet("/lobby/partybulk")]
    public IActionResult GetPartyBulk([FromQuery] string playerIds)
    {
        // Game joins player IDs with '+' separator, hard to test solo
        var ids = playerIds.Split([',', '+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var players = new Dictionary<string, LobbyInfoResponse>();

        foreach (var pid in ids)
        {
            var partyInfo = lobby.GetPlayerPartyInfo(pid);
            players[pid] = partyInfo;
        }

        return Ok(new LobbyGetBulkPlayerPartyInfoResponse
        {
            Players = players
        });
    }
}
