namespace OpenRocketArena.Server.Entities;

public class Quest
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public int SlotId { get; set; }
    public string QuestId { get; set; } = Guid.NewGuid().ToString();
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

    public Account Account { get; set; } = null!;
}
