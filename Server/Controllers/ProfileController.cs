using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;
using OpenRocketArena.Server.Entities;
using OpenRocketArena.Server.Models;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Profile endpoints
/// </summary>
[ApiController]
public class ProfileController(AppDbContext db, CmsProgressionData progression, CmsMatchmakingData matchmakingData, ILogger<ProfileController> logger) : ControllerBase
{
    [HttpGet("/profile/v2/profile/bulk")]
    public async Task<IActionResult> GetBulkProfile([FromQuery] string PlayerIds)
    {
        var playerIds = PlayerIds.Split([',', '+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var profiles = new List<ProfileDto>();

        foreach (var pid in playerIds)
        {
            var parts = pid.Split(':');
            if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId)) continue;

            var profile = await db.PlayerProfiles
                .AsNoTrackingWithIdentityResolution()
                .Include(p => p.CharacterProgressions).ThenInclude(c => c.EquipmentSets).ThenInclude(es => es.Items)
                .Include(p => p.CharacterProgressions).ThenInclude(c => c.Emotes)
                .Include(p => p.Equipment)
                .Include(p => p.ItemLevels)
                .Include(p => p.PlaylistRankings)
                .Include(p => p.BlastPassLevels)
                .Include(p => p.MotdViews)
                .FirstOrDefaultAsync(p => p.AccountId == accountId);

            if (profile == null)
            {
                profile = new PlayerProfile { AccountId = accountId };
                db.PlayerProfiles.Add(profile);
                await db.SaveChangesAsync();
            }

            profile.StatGroups = await db.PlayerStatGroups
                .AsNoTracking()
                .Include(sg => sg.Stats)
                .Where(sg => sg.ProfileId == profile.Id)
                .ToListAsync();

            profiles.Add(MapProfile(profile, pid));
        }

        return Ok(new { Profiles = profiles });
    }

    [HttpGet("/profile/v2/profile/{playerId}")]
    public async Task<IActionResult> GetProfile(string playerId)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
            return NotFound(new { error = "account_not_found" });

        // Load profile without stats to avoid cartesian explosion
        var profile = await db.PlayerProfiles
            .AsNoTrackingWithIdentityResolution()
            .Include(p => p.CharacterProgressions).ThenInclude(c => c.EquipmentSets).ThenInclude(es => es.Items)
            .Include(p => p.CharacterProgressions).ThenInclude(c => c.Emotes)
            .Include(p => p.Equipment)
            .Include(p => p.ItemLevels)
            .Include(p => p.PlaylistRankings)
            .Include(p => p.BlastPassLevels)
            .Include(p => p.MotdViews)
            .FirstOrDefaultAsync(p => p.AccountId == accountId);

        if (profile == null)
        {
            profile = new PlayerProfile { AccountId = accountId };
            db.PlayerProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        // Load stats separately to avoid cartesian explosion
        profile.StatGroups = await db.PlayerStatGroups
            .AsNoTracking()
            .Include(sg => sg.Stats)
            .Where(sg => sg.ProfileId == profile.Id)
            .ToListAsync();

        return Ok(new ProfileResponse { Profile = MapProfile(profile, playerId) });
    }

    [HttpPut("/profile/profile/{playerId}/motd/{motdId}/viewed")]
    public async Task<IActionResult> MarkMotdViewed(string playerId, string motdId)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var profile = await db.PlayerProfiles
            .Include(p => p.MotdViews)
            .FirstOrDefaultAsync(p => p.AccountId == accountId);

        if (profile == null)
        {
            profile = new PlayerProfile { AccountId = accountId };
            db.PlayerProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        if (!profile.MotdViews.Any(m => m.MotdId == motdId))
        {
            profile.MotdViews.Add(new MotdView { ProfileId = profile.Id, MotdId = motdId });
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPut("/profile/v2/profile/{playerId}/equip")]
    public async Task<IActionResult> Equip(string playerId, [FromBody] EquipPayload payload)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var profile = await db.PlayerProfiles
            .Include(p => p.CharacterProgressions).ThenInclude(c => c.EquipmentSets).ThenInclude(es => es.Items)
            .Include(p => p.CharacterProgressions).ThenInclude(c => c.Emotes)
            .Include(p => p.Equipment)
            .FirstOrDefaultAsync(p => p.AccountId == accountId);

        if (profile == null)
        {
            profile = new PlayerProfile { AccountId = accountId };
            db.PlayerProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        // Process character equips
        foreach (var charEquip in payload.CharacterEquip)
        {
            var cp = profile.CharacterProgressions.FirstOrDefault(c => c.CharacterId == charEquip.CharacterId);
            if (cp == null)
            {
                cp = new CharacterProgression { ProfileId = profile.Id, CharacterId = charEquip.CharacterId };
                profile.CharacterProgressions.Add(cp);
                await db.SaveChangesAsync();
            }

            // Upsert equipment sets - match by Name+IsRanked, merge items by ItemType
            foreach (var esReq in charEquip.EquipmentSets)
            {
                var es = cp.EquipmentSets.FirstOrDefault(e => e.Name == esReq.Name && e.IsRanked == esReq.IsRanked);
                if (es == null)
                {
                    es = new EquipmentSet
                    {
                        CharacterProgressionId = cp.Id,
                        Name = esReq.Name,
                        IsRanked = esReq.IsRanked
                    };
                    cp.EquipmentSets.Add(es);
                    await db.SaveChangesAsync();
                }

                foreach (var item in esReq.Equipment)
                {
                    var existingItem = es.Items.FirstOrDefault(i => i.ItemType == item.ItemType);
                    if (existingItem != null)
                    {
                        existingItem.ItemId = item.ItemId;
                    }
                    else
                    {
                        es.Items.Add(new EquipmentSetItem
                        {
                            EquipmentSetId = es.Id,
                            ItemId = item.ItemId,
                            ItemType = item.ItemType
                        });
                    }
                }
            }

            // Upsert emotes - merge by slot within each type
            if (charEquip.PreGameEmotes is { Count: > 0 })
            {
                foreach (var e in charEquip.PreGameEmotes)
                    UpsertEmote(cp, e.ItemId, e.Slot, EmoteType.PreGame);
            }

            if (charEquip.ChatEmotes is { Count: > 0 })
            {
                foreach (var e in charEquip.ChatEmotes)
                    UpsertEmote(cp, e.ItemId, e.Slot, EmoteType.Chat);
            }

            if (charEquip.VictoryPose != null && !string.IsNullOrEmpty(charEquip.VictoryPose.ItemId))
            {
                var existing = cp.Emotes.Where(e => e.EmoteType == EmoteType.VictoryPose).ToList();
                db.CharacterEmotes.RemoveRange(existing);
                cp.Emotes.Add(new CharacterEmote { CharacterProgressionId = cp.Id, ItemId = charEquip.VictoryPose.ItemId, EmoteType = EmoteType.VictoryPose });
            }
        }

        // Upsert player-level equips - merge by ItemType
        logger.LogInformation("Equip: {Count} existing profile equips: [{Types}]", profile.Equipment.Count, string.Join(", ", profile.Equipment.Select(e => $"{e.ItemType}={e.ItemId}")));
        logger.LogInformation("Equip: {Count} incoming playerEquip: [{Types}]", payload.PlayerEquip.Count, string.Join(", ", payload.PlayerEquip.Select(e => $"{e.ItemType}={e.ItemId}")));

        foreach (var item in payload.PlayerEquip)
        {
            var existing = profile.Equipment.FirstOrDefault(e => e.ItemType == item.ItemType);
            if (existing != null)
            {
                existing.ItemId = item.ItemId;
            }
            else
            {
                profile.Equipment.Add(new ProfileEquipItem
                {
                    ProfileId = profile.Id,
                    ItemId = item.ItemId,
                    ItemType = item.ItemType
                });
            }
        }

        await db.SaveChangesAsync();
        return Ok(new ProfileResponse { Profile = MapProfile(profile, playerId) });
    }

    private static void UpsertEmote(CharacterProgression cp, string itemId, int slot, EmoteType emoteType)
    {
        var existing = cp.Emotes.FirstOrDefault(e => e.EmoteType == emoteType && e.Slot == slot);
        if (existing != null)
        {
            existing.ItemId = itemId;
        }
        else
        {
            cp.Emotes.Add(new CharacterEmote { CharacterProgressionId = cp.Id, ItemId = itemId, Slot = slot, EmoteType = emoteType });
        }
    }

    private static ProfileDto MapProfile(PlayerProfile p, string playerId)
    {
        return new ProfileDto
        {
            MangoId = playerId,
            GamesPlayed = p.GamesPlayed,
            ArtifactUnlockLevel = p.ArtifactUnlockLevel,
            ArtifactUnlockProgress = p.ArtifactUnlockProgress,
            ArtifactUnlockXp = p.ArtifactUnlockXp,
            CareerLevel = p.CareerLevel,
            CareerXp = p.CareerXp,
            Progress = p.Progress,
            LastPlayedMatchID = p.LastPlayedMatchId,
            ActiveMatchID = p.ActiveMatchId,
            ActiveMatchIDUpdatedAt = p.ActiveMatchIdUpdatedAt == default ? "0001-01-01T00:00:00Z" : p.ActiveMatchIdUpdatedAt.ToString("O"),
            OnboardingState = p.OnboardingState,
            AdvertState = p.AdvertState,
            PromosOwned = p.PromosOwned,
            BanLevel = p.BanLevel,
            UnbanTime = p.UnbanTime == default ? "0001-01-01T00:00:00+00:00" : p.UnbanTime.ToString("O"),
            BanMock = p.BanMock,

            CharacterProgression = p.CharacterProgressions.Select(c => MapCharacterProgression(c, playerId)).ToList(),
            Equipment = p.Equipment.Select(e => new EquipItemDto { ItemId = e.ItemId, ItemType = e.ItemType }).ToList(),
            ItemLevels = p.ItemLevels.Select(il => new ItemLevelDto
            {
                UserId = playerId,
                ItemId = il.ItemId,
                Level = il.Level,
                TotalXp = il.TotalXp,
                Progress = il.Progress
            }).ToList(),
            PlaylistRankings = p.PlaylistRankings.Select(pr => new PlaylistRankDto
            {
                PlayerId = playerId,
                PlaylistId = pr.PlaylistId,
                SkillMean = pr.SkillMean,
                SkillStdDev = pr.SkillStdDev,
                Rank = pr.Rank,
                GamesPlayed = pr.GamesPlayed,
                GamesWon = pr.GamesWon,
                GamesQuit = pr.GamesQuit,
                Streak = pr.Streak,
                BotLevel = pr.BotLevel,
                CreatedAt = pr.CreatedAt.ToString("O"),
                UpdatedAt = pr.UpdatedAt.ToString("O")
            }).ToList(),
            BlastPassLevels = p.BlastPassLevels.Select(bp => new BlastPassProgressionDto
            {
                BlastPassId = bp.BlastPassId,
                BlastPassXp = bp.BlastPassXp,
                BlastPassLevel = bp.BlastPassLevel,
                BpProgress = bp.BpProgress,
                XpBonus = bp.XpBonus,
                PartyXpBonus = bp.PartyXpBonus,
                Viewed = bp.Viewed
            }).ToList(),
            // Ranks derived from PlaylistRankings
            Ranks = p.PlaylistRankings.ToDictionary(pr => pr.PlaylistId, pr => (float)pr.Rank),
            MotdViews = p.MotdViews.Select(m => m.MotdId).ToList(),
            Stats = MapStats(p, playerId)
        };
    }

    // --- Match History ---

    [HttpPost("/profile/matches")]
    public async Task<IActionResult> PostMatchEnd([FromBody] MatchData request)
    {
        var rawJson = System.Text.Json.JsonSerializer.Serialize(request);

        foreach (var player in request.Players)
        {
            if (player.IsABot) continue;

            var parts = player.MangoId.Split(':');
            if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId)) continue;

            // Store match history
            db.MatchHistories.Add(new MatchHistory
            {
                MatchId = request.MatchId,
                AccountId = accountId,
                PlayerId = player.MangoId,
                MatchEndJsonData = rawJson
            });

            // Update profile
            var profile = await db.PlayerProfiles
                .AsSplitQuery()
                .Include(p => p.StatGroups).ThenInclude(sg => sg.Stats)
                .FirstOrDefaultAsync(p => p.AccountId == accountId);

            if (profile == null) continue;

            profile.GamesPlayed++;
            profile.LastPlayedMatchId = request.MatchId;

            var isWin = player.GameOutcome == "Win";
            var isQuit = player.GameOutcome == "Quit";
            var isDraw = player.GameOutcome == "Draw";

            // Calculate XP earned
            var matchXp = progression.CalculateMatchXp(request.MatchTime, isWin);

            // Update character progression + XP
            var cp = await db.CharacterProgressions
                .FirstOrDefaultAsync(c => c.ProfileId == profile.Id && c.CharacterId == player.CharacterId);

            if (cp == null)
            {
                cp = new CharacterProgression
                {
                    ProfileId = profile.Id,
                    CharacterId = player.CharacterId,
                    Level = 1
                };
                db.CharacterProgressions.Add(cp);
                await db.SaveChangesAsync();
            }

            cp.GamesPlayed++;
            var oldLevel = cp.Level;
            cp.Experience += matchXp;
            var (newLevel, charProgress) = progression.GetCharacterLevel(cp.Experience);
            cp.Level = newLevel;
            cp.Progress = charProgress;

            // Award level-up rewards
            if (newLevel > oldLevel)
            {
                var inventory = await db.PlayerInventories
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.AccountId == accountId);

                if (inventory != null)
                {
                    // Rocket parts
                    var partsEarned = progression.GetPartsForLevelUp(oldLevel, newLevel);
                    if (partsEarned > 0)
                        inventory.RocketParts += partsEarned;

                    // Treasure box items (character-specific rewards)
                    var treasureItems = progression.GetTreasureBoxRewards(player.CharacterId, oldLevel, newLevel);
                    foreach (var (itemId, itemType) in treasureItems)
                    {
                        if (!inventory.Items.Any(i => i.CmsItemId == itemId))
                        {
                            inventory.Items.Add(new InventoryItem
                            {
                                InventoryId = inventory.Id,
                                CmsItemId = itemId,
                                ItemCategory = itemType,
                                Viewed = false,
                                PopUpNotification = true
                            });
                        }
                    }

                    logger.LogInformation("Player {PlayerId} leveled {Character} {OldLevel}->{NewLevel}, earned {Parts} parts + {Items} items", player.MangoId, player.CharacterId, oldLevel, newLevel, partsEarned, treasureItems.Count);
                }
            }

            // Update artifact unlock XP
            var oldArtLevel = profile.ArtifactUnlockLevel;
            profile.ArtifactUnlockXp += (int)(matchXp * progression.XpConfig.ArtifactUnlockMultiplier);
            var (artLevel, artProgress) = progression.GetArtifactUnlockLevel(profile.ArtifactUnlockXp);
            profile.ArtifactUnlockLevel = artLevel;
            profile.ArtifactUnlockProgress = artProgress;

            // Grant artifact unlock rewards
            if (artLevel > oldArtLevel)
            {
                var artifactRewards = progression.GetArtifactUnlockRewards(oldArtLevel, artLevel);
                if (artifactRewards.Count > 0)
                {
                    var inventory = await db.PlayerInventories
                        .Include(i => i.Items)
                        .FirstOrDefaultAsync(i => i.AccountId == accountId);

                    if (inventory != null)
                    {
                        foreach (var (itemId, itemType, _) in artifactRewards)
                        {
                            if (!inventory.Items.Any(i => i.CmsItemId == itemId))
                            {
                                inventory.Items.Add(new InventoryItem
                                {
                                    InventoryId = inventory.Id,
                                    CmsItemId = itemId,
                                    ItemCategory = itemType,
                                    Viewed = false,
                                    PopUpNotification = true
                                });
                            }
                        }

                        logger.LogInformation("Player {PlayerId} artifact level {OldLevel}->{NewLevel}, granted {Count} artifacts", player.MangoId, oldArtLevel, artLevel, artifactRewards.Count);
                    }
                }
            }

            // Update item levels for equipped artifacts
            var itemXp = (int)(matchXp * progression.XpConfig.ItemLevelMultiplier);
            if (itemXp > 0 && player.EquippedArtifacts.Count > 0)
            {
                var itemLevels = await db.ItemLevels.Where(il => il.ProfileId == profile.Id).ToListAsync();

                foreach (var artifact in player.EquippedArtifacts)
                {
                    if (string.IsNullOrEmpty(artifact.ArtifactId) || !progression.HasItemLeveling(artifact.ArtifactType))
                        continue;

                    var il = itemLevels.FirstOrDefault(l => l.ItemId == artifact.ArtifactId);
                    if (il == null)
                    {
                        il = new ItemLevel
                        {
                            ProfileId = profile.Id,
                            ItemId = artifact.ArtifactId,
                        };
                        db.ItemLevels.Add(il);
                        itemLevels.Add(il);
                    }

                    il.TotalXp += itemXp;
                    var (itemLevel, itemProgress) = progression.GetItemLevel(artifact.ArtifactType, il.TotalXp);
                    il.Level = itemLevel;
                    il.Progress = itemProgress;
                }
            }

            // Update playlist ranking
            if (!string.IsNullOrEmpty(request.PlaylistUniqueId))
            {
                var ranking = await db.PlaylistRankings
                    .FirstOrDefaultAsync(r => r.ProfileId == profile.Id && r.PlaylistId == request.PlaylistUniqueId);

                if (ranking == null)
                {
                    ranking = new PlaylistRanking
                    {
                        ProfileId = profile.Id,
                        PlaylistId = request.PlaylistUniqueId,
                        SkillMean = RatingService.InitialMean,
                        SkillStdDev = RatingService.InitialStdDev,
                        Rank = 1.0f,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.PlaylistRankings.Add(ranking);
                }

                ranking.GamesPlayed++;
                if (isWin) { ranking.GamesWon++; ranking.Streak++; }
                else if (isQuit) { ranking.GamesQuit++; ranking.Streak = 0; }
                else { ranking.Streak = 0; }

                var (newMean, newStdDev) = RatingService.UpdateRating(
                    ranking.SkillMean, ranking.SkillStdDev, isWin, isDraw);

                ranking.SkillMean = newMean;
                ranking.SkillStdDev = newStdDev;
                ranking.Rank = RatingService.CalculateRank(
                    newMean, newStdDev, ranking.GamesPlayed, ranking.GamesWon,
                    (int)progression.XpConfig.RankConvergence, (int)progression.XpConfig.RankLogBase);
                var botThresholds = matchmakingData.GetBotLevelsForPlaylist(request.PlaylistUniqueId);
                ranking.BotLevel = RatingService.CalculateBotLevel(ranking.SkillMean, botThresholds);
                ranking.UpdatedAt = DateTime.UtcNow;
            }

            // Update stat slices: lifetime, character, map, mode, playlist
            var slices = new List<(string type, string value)>
            {
                ("lifetime", ""),
                ("character", player.CharacterId),
                ("map", request.Map),
                ("mode", request.Mode),
                ("playlist", request.PlaylistUniqueId)
            };

            foreach (var (sliceType, sliceValue) in slices)
            {
                var group = profile.StatGroups.FirstOrDefault(sg => sg.SliceType == sliceType && sg.SliceValue == sliceValue);

                if (group == null)
                {
                    group = new PlayerStatGroup
                    {
                        ProfileId = profile.Id,
                        SliceType = sliceType,
                        SliceValue = sliceValue
                    };
                    profile.StatGroups.Add(group);
                    await db.SaveChangesAsync(); // get group.Id
                }

                group.GamesPlayed++;
                if (isWin) group.GamesWon++;
                if (isQuit) group.GamesQuit++;
                if (isDraw) group.GamesDrawn++;

                // Update per-metric aggregates
                foreach (var (metric, value) in player.Stats)
                {
                    var entry = group.Stats.FirstOrDefault(s => s.Metric == metric);
                    if (entry == null)
                    {
                        entry = new PlayerStatEntry
                        {
                            StatGroupId = group.Id,
                            Metric = metric,
                            Min = value,
                            Max = value,
                            Sum = value,
                            Count = 1
                        };
                        group.Stats.Add(entry);
                    }
                    else
                    {
                        if (value < entry.Min) entry.Min = value;
                        if (value > entry.Max) entry.Max = value;
                        entry.Sum += value;
                        entry.Count++;
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Match {MatchId} recorded with {PlayerCount} players", request.MatchId, request.Players.Count);

        return Ok();
    }

    [HttpGet("/profile/matches/{matchId}")]
    public async Task<IActionResult> GetMatch(string matchId)
    {
        var match = await db.MatchHistories.FirstOrDefaultAsync(m => m.MatchId == matchId);

        if (match == null)
            return NotFound(new { error = "match_not_found" });

        var matchData = System.Text.Json.JsonSerializer.Deserialize<MatchData>(match.MatchEndJsonData);
        return Ok(matchData);
    }

    [HttpGet("/profile/matches/history/{playerId}")]
    public async Task<IActionResult> GetMatchHistory(string playerId)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var lastMatch = await db.MatchHistories.Where(m => m.AccountId == accountId).OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync();

        if (lastMatch == null)
        {
            return Ok(new GetMatchHistoryResponse
            {
                MatchHistory = new MatchResultDto()
            });
        }

        return Ok(new GetMatchHistoryResponse
        {
            MatchHistory = new MatchResultDto
            {
                MatchId = lastMatch.MatchId,
                MatchEndJsonData = lastMatch.MatchEndJsonData
            }
        });
    }

    private static StatsDto MapStats(PlayerProfile p, string playerId)
    {
        StatsGroupDto MapGroup(PlayerStatGroup g) => new()
        {
            PlayerID = playerId,
            SliceType = g.SliceType,
            SliceValue = g.SliceValue,
            GamesPlayed = g.GamesPlayed,
            GamesWon = g.GamesWon,
            GamesQuit = g.GamesQuit,
            GamesDrawn = g.GamesDrawn,
            Stats = g.Stats.Select(s => new StatEntryDto
            {
                Metric = s.Metric,
                Min = s.Min,
                Max = s.Max,
                Mean = s.Count > 0 ? s.Sum / s.Count : 0,
                Sum = s.Sum
            }).ToList()
        };

        var lifetime = p.StatGroups.FirstOrDefault(sg => sg.SliceType == "lifetime");

        Dictionary<string, StatsGroupDto> MapSlice(string sliceType) =>
            p.StatGroups.Where(sg => sg.SliceType == sliceType).ToDictionary(sg => sg.SliceValue, MapGroup);

        return new StatsDto
        {
            Lifetime = lifetime != null ? MapGroup(lifetime) : new StatsGroupDto { PlayerID = playerId, SliceType = "lifetime" },
            SlicedStats = new SlicedStatsDto
            {
                Blastpass = MapSlice("blastpass"),
                Character = MapSlice("character"),
                Map = MapSlice("map"),
                Mode = MapSlice("mode"),
                Playlist = MapSlice("playlist")
            }
        };
    }

    private static CharacterProgressionDto MapCharacterProgression(CharacterProgression c, string playerId)
    {
        var preGameEmotes = c.Emotes.Where(e => e.EmoteType == EmoteType.PreGame).ToList();
        var chatEmotes = c.Emotes.Where(e => e.EmoteType == EmoteType.Chat).ToList();
        var victoryPose = c.Emotes.FirstOrDefault(e => e.EmoteType == EmoteType.VictoryPose);

        return new CharacterProgressionDto
        {
            MangoId = playerId,
            CharacterId = c.CharacterId,
            GamesPlayed = c.GamesPlayed,
            Level = c.Level,
            Experience = c.Experience,
            Progress = c.Progress,
            LastPlayedMatchId = c.LastPlayedMatchId,
            EquipmentSets = c.EquipmentSets.Select(es => new EquipmentSetDto
            {
                Id = (int)es.Id,
                Name = es.Name,
                CharacterId = c.CharacterId,
                PlayerId = playerId,
                IsRanked = es.IsRanked,
                Equipment = es.Items.Select(i => new EquipItemDto { ItemId = i.ItemId, ItemType = i.ItemType }).ToList()
            }).ToList(),
            PreGameEmotes = preGameEmotes.Count > 0 ? preGameEmotes.Select(e => new EmoteWithSlotDto { ItemId = e.ItemId, Slot = e.Slot }).ToList() : null,
            ChatEmotes = chatEmotes.Count > 0 ? chatEmotes.Select(e => new EmoteWithSlotDto { ItemId = e.ItemId, Slot = e.Slot }).ToList() : null,
            VictoryPose = victoryPose != null ? new EmoteDto { ItemId = victoryPose.ItemId } : null
        };
    }
}
