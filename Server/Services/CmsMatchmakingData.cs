using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Services;

public class CmsMatchmakingData
{
    private readonly ILogger<CmsMatchmakingData> _logger;
    private Dictionary<string, CmsPlaylist> _playlists = new();
    private Dictionary<string, CmsMap> _maps = new();
    private Dictionary<string, CmsMode> _modes = new();
    private List<BotLevelThreshold> _botLevelsPvp = [];
    private List<BotLevelThreshold> _botLevelsPve = [];

    public List<BotLevelThreshold> BotLevelsPvp => _botLevelsPvp;
    public List<BotLevelThreshold> BotLevelsPve => _botLevelsPve;

    public List<BotLevelThreshold> GetBotLevelsForPlaylist(string playlistId)
    {
        var playlist = GetPlaylist(playlistId);
        return playlist is { IsPveOnly: true } ? _botLevelsPve : _botLevelsPvp;
    }

    public CmsMatchmakingData(IConfiguration config, IWebHostEnvironment env, ILogger<CmsMatchmakingData> logger)
    {
        _logger = logger;
        var version = config["Cms:Versions:stage"] ?? "1.default";
        var path = Path.Combine(env.WebRootPath, "cms", version, "matchmaking.json");
        if (File.Exists(path))
            Load(path);
        else
            logger.LogWarning("matchmaking.json not found at {Path}", path);
    }

    private void Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CmsMatchmakingDocument>(json, options);
        if (doc == null) return;

        _playlists = doc.Playlist.ToDictionary(p => p.UniqueId);
        _maps = doc.Map.ToDictionary(m => m.UniqueId);
        _modes = doc.Mode.ToDictionary(m => m.UniqueId);
        _botLevelsPvp = doc.BotLevelsPvp
            .Select(b => new BotLevelThreshold
            {
                BotLevel = int.TryParse(b.BotLevel, out var bl) ? bl : 0,
                SkillMin = b.SkillMin
            })
            .OrderByDescending(b => b.SkillMin)
            .ToList();
        _botLevelsPve = doc.BotLevelsPve
            .Select(b => new BotLevelThreshold
            {
                BotLevel = int.TryParse(b.BotLevel, out var bl) ? bl : 2,
                SkillMin = 0
            })
            .ToList();

        _logger.LogInformation("Loaded matchmaking CMS: {Playlists} playlists, {Maps} maps, {Modes} modes", _playlists.Count, _maps.Count, _modes.Count);
    }

    public CmsPlaylist? GetPlaylist(string id) => _playlists.GetValueOrDefault(id);
    public CmsMap? GetMap(string id) => _maps.GetValueOrDefault(id);
    public CmsMode? GetMode(string id) => _modes.GetValueOrDefault(id);

    /// <summary>
    /// Pick a random map+mode from the playlist's map_mode_list.
    /// </summary>
    public (CmsMap? map, CmsMode? mode) PickRandomMapMode(string playlistId)
    {
        var playlist = GetPlaylist(playlistId);
        if (playlist == null || playlist.MapModeList.Count == 0)
            return (null, null);

        var pick = playlist.MapModeList[Random.Shared.Next(playlist.MapModeList.Count)];
        return (GetMap(pick.Map), GetMode(pick.Mode));
    }
}

// --- CMS deserialization models ---

public class CmsMatchmakingDocument
{
    [JsonPropertyName("playlist")]
    public List<CmsPlaylist> Playlist { get; set; } = [];

    [JsonPropertyName("botlevelspve")]
    public List<CmsBotLevelPvp> BotLevelsPve { get; set; } = [];

    [JsonPropertyName("botlevelspvp")]
    public List<CmsBotLevelPvp> BotLevelsPvp { get; set; } = [];

    [JsonPropertyName("map")]
    public List<CmsMap> Map { get; set; } = [];

    [JsonPropertyName("mode")]
    public List<CmsMode> Mode { get; set; } = [];
}

public class CmsPlaylist
{
    [JsonPropertyName("unique_id")]
    public string UniqueId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("gamelift_id")]
    public string GameliftId { get; set; } = string.Empty;

    [JsonPropertyName("fill_with_bots")]
    public bool FillWithBots { get; set; }

    [JsonPropertyName("target_player_count")]
    public int TargetPlayerCount { get; set; } = 6;

    [JsonPropertyName("max_party_size")]
    public int MaxPartySize { get; set; } = 3;

    [JsonPropertyName("is_ranked")]
    public bool IsRanked { get; set; }

    [JsonPropertyName("is_pve_only")]
    public bool IsPveOnly { get; set; }

    [JsonPropertyName("map_mode_list")]
    public List<CmsMapMode> MapModeList { get; set; } = [];
}

public class CmsMapMode
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;
}

public class CmsMap
{
    [JsonPropertyName("unique_id")]
    public string UniqueId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("engine_asset_id")]
    public string EngineAssetId { get; set; } = string.Empty;
}

public class CmsMode
{
    [JsonPropertyName("unique_id")]
    public string UniqueId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("engine_asset_id")]
    public string EngineAssetId { get; set; } = string.Empty;
}

public class CmsBotLevelPvp
{
    [JsonPropertyName("bot_level")]
    public string BotLevel { get; set; } = "0";

    [JsonPropertyName("skill_min")]
    public float SkillMin { get; set; }
}
