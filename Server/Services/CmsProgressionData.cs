using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Services;

public class CmsProgressionData
{
    private readonly ILogger<CmsProgressionData> _logger;
    private List<CmsCharacterLevel> _characterLevels = [];
    private List<CmsArtifactUnlockLevel> _artifactLevels = [];
    private List<CmsItemLevel> _itemLevels = [];
    private CmsXpConfig _xpConfig = new();

    public CmsXpConfig XpConfig => _xpConfig;

    public CmsProgressionData(IConfiguration config, IWebHostEnvironment env, ILogger<CmsProgressionData> logger)
    {
        _logger = logger;
        var version = config["Cms:Versions:stage"] ?? "1.default";
        var path = Path.Combine(env.WebRootPath, "cms", version, "progression.json");
        if (File.Exists(path))
            Load(path);
        else
            logger.LogWarning("progression.json not found at {Path}", path);
    }

    private void Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CmsProgressionDocument>(json, options);
        if (doc == null) return;

        _characterLevels = doc.CharacterProgression.OrderBy(c => c.Level).ToList();
        _artifactLevels = doc.ArtifactUnlockLevel.OrderBy(a => a.Level).ToList();
        _itemLevels = doc.ItemLevel.OrderBy(i => i.Level).ToList();
        _xpConfig = doc.XpConfig.FirstOrDefault() ?? new CmsXpConfig();

        _logger.LogInformation("Loaded progression CMS: {CharLevels} character levels, {ArtifactLevels} artifact levels, xp_per_second={XpPerSec} xp_for_win={XpForWin}", _characterLevels.Count, _artifactLevels.Count, _xpConfig.XpPerSecond, _xpConfig.XpForWin);
    }

    /// <summary>
    /// Calculate XP earned for a match.
    /// </summary>
    public int CalculateMatchXp(int matchTimeSeconds, bool isWin)
    {
        var xp = _xpConfig.XpPerSecond * matchTimeSeconds;
        if (isWin)
            xp += _xpConfig.XpForWin;
        return xp;
    }

    /// <summary>
    /// Given total character XP, returns the level and progress within that level.
    /// </summary>
    public (int level, float progress) GetCharacterLevel(int totalXp)
    {
        if (_characterLevels.Count == 0)
            return (1, 0);

        for (int i = _characterLevels.Count - 1; i >= 0; i--)
        {
            var lvl = _characterLevels[i];
            if (totalXp >= lvl.XpStart)
            {
                if (lvl.XpEnd < 0) // max level
                    return (lvl.Level, 0);

                var range = lvl.XpEnd - lvl.XpStart + 1;
                var into = totalXp - lvl.XpStart;
                var progress = range > 0 ? (float)into / range : 0;
                return (lvl.Level, progress);
            }
        }

        return (1, 0);
    }

    /// <summary>
    /// Returns rocket parts earned for leveling from oldLevel to newLevel.
    /// </summary>
    public int GetPartsForLevelUp(int oldLevel, int newLevel)
    {
        var parts = 0;
        foreach (var lvl in _characterLevels)
        {
            if (lvl.Level > oldLevel && lvl.Level <= newLevel)
                parts += lvl.RocketParts;
        }
        return parts;
    }

    /// <summary>
    /// Given total artifact XP, returns the artifact unlock level.
    /// </summary>
    public (int level, float progress) GetArtifactUnlockLevel(int totalXp)
    {
        if (_artifactLevels.Count == 0)
            return (0, 0);

        for (int i = _artifactLevels.Count - 1; i >= 0; i--)
        {
            var lvl = _artifactLevels[i];
            if (totalXp >= lvl.XpStart)
            {
                if (lvl.XpEnd < 0)
                    return (lvl.Level, 0);

                var range = lvl.XpEnd - lvl.XpStart + 1;
                var into = totalXp - lvl.XpStart;
                var progress = range > 0 ? (float)into / range : 0;
                return (lvl.Level, progress);
            }
        }

        return (0, 0);
    }

    /// <summary>
    /// Returns treasure box items granted for leveling a specific character from oldLevel to newLevel.
    /// Each treasure_box has per-character members with direct_items.
    /// </summary>
    public List<(string itemId, string itemType)> GetTreasureBoxRewards(string characterId, int oldLevel, int newLevel)
    {
        var items = new List<(string, string)>();
        foreach (var lvl in _characterLevels)
        {
            if (lvl.Level <= oldLevel || lvl.Level > newLevel) continue;
            if (lvl.TreasureBox == null) continue;

            foreach (var member in lvl.TreasureBox.Members)
            {
                if (member.Character != characterId) continue;
                foreach (var item in member.DirectItems)
                    items.Add((item.LookupField, item.ItemType));
            }
        }
        return items;
    }

    /// <summary>
    /// Returns artifacts granted for artifact unlock leveling from oldLevel to newLevel.
    /// </summary>
    public List<(string itemId, string itemType, bool equipOnAcquire)> GetArtifactUnlockRewards(int oldLevel, int newLevel)
    {
        var items = new List<(string, string, bool)>();
        foreach (var lvl in _artifactLevels)
        {
            if (lvl.Level <= oldLevel || lvl.Level > newLevel) continue;
            if (lvl.GrantedArtifact == null) continue;
            items.Add((lvl.GrantedArtifact.LookupField, lvl.GrantedArtifact.ItemType, lvl.GrantedArtifact.EquipOnAcquire));
        }
        return items;
    }

    /// <summary>
    /// Given total XP for an item of a specific type, returns the level and progress.
    /// </summary>
    public (int level, float progress) GetItemLevel(string itemType, float totalXp)
    {
        var levels = _itemLevels
            .Where(l => l.ItemType == itemType)
            .OrderByDescending(l => l.Level)
            .ToList();

        foreach (var lvl in levels)
        {
            if (totalXp >= lvl.XpStart)
            {
                if (lvl.XpEnd < 0)
                    return (lvl.Level, 0);

                var range = lvl.XpEnd - lvl.XpStart + 1;
                var into = totalXp - lvl.XpStart;
                return (lvl.Level, range > 0 ? into / range : 0);
            }
        }

        return (1, 0);
    }

    /// <summary>
    /// Check if an item type has level progression.
    /// </summary>
    public bool HasItemLeveling(string itemType) => _itemLevels.Any(l => l.ItemType == itemType);
}

// --- CMS models ---

public class CmsProgressionDocument
{
    [JsonPropertyName("character_progression")]
    public List<CmsCharacterLevel> CharacterProgression { get; set; } = [];

    [JsonPropertyName("artifact_unlock_level")]
    public List<CmsArtifactUnlockLevel> ArtifactUnlockLevel { get; set; } = [];

    [JsonPropertyName("item_level")]
    public List<CmsItemLevel> ItemLevel { get; set; } = [];

    [JsonPropertyName("xp_config")]
    public List<CmsXpConfig> XpConfig { get; set; } = [];
}

public class CmsCharacterLevel
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("xp_start")]
    public int XpStart { get; set; }

    [JsonPropertyName("xp_end")]
    public int XpEnd { get; set; }

    [JsonPropertyName("rocket_parts")]
    public int RocketParts { get; set; }

    [JsonPropertyName("rocket_fuel")]
    public int RocketFuel { get; set; }

    [JsonPropertyName("treasure_box_award")]
    public CmsTreasureBox? TreasureBox { get; set; }
}

public class CmsTreasureBox
{
    [JsonPropertyName("lookup_field")]
    public string LookupField { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<CmsTreasureBoxMember> Members { get; set; } = [];
}

public class CmsTreasureBoxMember
{
    [JsonPropertyName("character")]
    public string Character { get; set; } = string.Empty;

    [JsonPropertyName("direct_items")]
    public List<CmsTreasureBoxItem> DirectItems { get; set; } = [];
}

public class CmsTreasureBoxItem
{
    [JsonPropertyName("lookup_field")]
    public string LookupField { get; set; } = string.Empty;

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = string.Empty;
}

public class CmsArtifactUnlockLevel
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("xp_start")]
    public int XpStart { get; set; }

    [JsonPropertyName("xp_end")]
    public int XpEnd { get; set; }

    [JsonPropertyName("granted_artifact")]
    public CmsGrantedArtifact? GrantedArtifact { get; set; }
}

public class CmsGrantedArtifact
{
    [JsonPropertyName("lookup_field")]
    public string LookupField { get; set; } = string.Empty;

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("equip_on_acquire")]
    public bool EquipOnAcquire { get; set; }
}

public class CmsItemLevel
{
    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("xp_start")]
    public float XpStart { get; set; }

    [JsonPropertyName("xp_end")]
    public float XpEnd { get; set; }
}

public class CmsXpConfig
{
    [JsonPropertyName("xp_per_second")]
    public int XpPerSecond { get; set; } = 5;

    [JsonPropertyName("xp_for_win")]
    public int XpForWin { get; set; } = 300;

    [JsonPropertyName("parts_per_xp")]
    public float PartsPerXp { get; set; }

    [JsonPropertyName("artifact_unlock_multiplier")]
    public float ArtifactUnlockMultiplier { get; set; } = 1;

    [JsonPropertyName("item_level_multiplier")]
    public float ItemLevelMultiplier { get; set; } = 1;

    [JsonPropertyName("rank_convergence")]
    public float RankConvergence { get; set; } = 350;

    [JsonPropertyName("rank_log_base")]
    public float RankLogBase { get; set; } = 55;
}
