namespace OpenRocketArena.Server.Entities;

public class PlaylistRanking
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string PlaylistId { get; set; } = string.Empty;
    public float SkillMean { get; set; }
    public float SkillStdDev { get; set; }
    public float Rank { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesQuit { get; set; }
    public int Streak { get; set; }
    public int BotLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public PlayerProfile Profile { get; set; } = null!;
}
