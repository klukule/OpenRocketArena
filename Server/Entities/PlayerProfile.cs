namespace OpenRocketArena.Server.Entities;

public class PlayerProfile
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public int GamesPlayed { get; set; }
    public int CareerLevel { get; set; }
    public int CareerXp { get; set; }
    public int ArtifactUnlockLevel { get; set; }
    public float ArtifactUnlockProgress { get; set; }
    public int ArtifactUnlockXp { get; set; }
    public float Progress { get; set; }
    public string LastPlayedMatchId { get; set; } = string.Empty;
    public string ActiveMatchId { get; set; } = string.Empty;
    public DateTime ActiveMatchIdUpdatedAt { get; set; }
    public int OnboardingState { get; set; } = -1;
    public bool AdvertState { get; set; }
    public string PromosOwned { get; set; } = string.Empty;
    public int BanLevel { get; set; }
    public DateTime UnbanTime { get; set; }
    public bool BanMock { get; set; }

    public Account Account { get; set; } = null!;
    public List<CharacterProgression> CharacterProgressions { get; set; } = [];
    public List<ProfileEquipItem> Equipment { get; set; } = [];
    public List<ItemLevel> ItemLevels { get; set; } = [];
    public List<PlaylistRanking> PlaylistRankings { get; set; } = [];
    public List<BlastPassProgression> BlastPassLevels { get; set; } = [];
    public List<MotdView> MotdViews { get; set; } = [];
    public List<PlayerStatGroup> StatGroups { get; set; } = [];
}
