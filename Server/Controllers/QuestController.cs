using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Quest endpoints
/// </summary>
[ApiController]
public class QuestController(AppDbContext db) : ControllerBase
{
    [HttpGet("/quest/{playerId}")]
    public async Task<IActionResult> GetQuests(string playerId)
    {
        // playerId is "accountId:personaId:1" - extract accountId
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var quests = await db.Quests.Where(q => q.AccountId == accountId).OrderBy(q => q.SlotId).ToListAsync();

        return Ok(new QuestListResponse
        {
            Quests = quests.Select(q => new QuestDto
            {
                SlotID = q.SlotId,
                QuestID = q.QuestId,
                Goal = q.Goal,
                Progress = q.Progress,
                State = q.State,
                StartTime = q.StartTime,
                EndTime = q.EndTime,
                ExpiresTime = q.ExpiresTime,
                DismissCooldown = q.DismissCooldown,
                LastDismissTime = q.LastDismissTime,
                LastUpdateTime = q.LastUpdateTime,
                SelectedRewardId = q.SelectedRewardId,
                BlastPassXpReward = q.BlastPassXpReward,
                UpdatedAt = q.UpdatedAt,
                CreatedAt = q.CreatedAt
            }).ToList()
        });
    }
}

public class QuestListResponse
{
    [JsonPropertyName("Quests")]
    public List<QuestDto> Quests { get; set; } = [];
}

public class QuestDto
{
    public int SlotID { get; set; }
    public string QuestID { get; set; } = string.Empty;
    public int Goal { get; set; }
    public int Progress { get; set; }
    public int State { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime ExpiresTime { get; set; }
    public int DismissCooldown { get; set; }
    public DateTime LastDismissTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public string SelectedRewardId { get; set; } = string.Empty;
    public int BlastPassXpReward { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
