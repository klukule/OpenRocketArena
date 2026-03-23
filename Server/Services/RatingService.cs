namespace OpenRocketArena.Server.Services;

/// <summary>
/// Rating calculations for Open Rocket Arena.
/// 
/// TODO: This needs complete rework - I just shoehorned TrueSkill like formulas to get something working,
/// game uses bespoke rating that I can't really figure out from the client.
/// </summary>
public static class RatingService
{
    // TrueSkill defaults (matching the game's initial values)
    public const float InitialMean = 25.0f;
    public const float InitialStdDev = 25.0f / 3.0f; // ~8.333
    private const float Beta = 25.0f / 6.0f;          // ~4.167 (performance variance)
    private const float DynamicsFactor = 25.0f / 300.0f; // tau - uncertainty increase per game

    /// <summary>
    /// Update a player's rating after a match result.
    /// </summary>
    public static (float newMean, float newStdDev) UpdateRating(float mean, float stdDev, bool isWin, bool isDraw, float opponentMean = InitialMean)
    {
        // Add dynamics factor
        var phi = (float)Math.Sqrt(stdDev * stdDev + DynamicsFactor * DynamicsFactor);

        // Combined variance
        var c = (float)Math.Sqrt(2.0 * Beta * Beta + phi * phi);

        // Expected score
        var diff = (mean - opponentMean) / c;
        var expected = Sigmoid(diff);

        // Actual score
        var actual = isWin ? 1.0f : (isDraw ? 0.5f : 0.0f);

        // Update factor
        var v = phi * phi / (c * c);

        // Update mean - scale by 2.0 for faster convergence
        var newMean = mean + v * (actual - expected) * c * 2.0f;

        // Update std dev - decrease with each game
        var newStdDev = (float)Math.Sqrt(phi * phi * (1.0 - v * 0.1));

        // Clamp to reasonable bounds
        newMean = Math.Clamp(newMean, 0.0f, 50.0f);
        newStdDev = Math.Clamp(newStdDev, 0.5f, InitialStdDev);

        return (newMean, newStdDev);
    }

    /// <summary>
    /// Calculate display rank using convergence and log_base tuning parameters.
    /// As games increase, confidence grows and rank reflects skill more accurately.
    /// Formula: rank = max(1, mu - k*sigma) where k decreases from 3 toward 0 as games approach convergence.
    /// For high-skill players with many games, rank can exceed mu via a win-rate bonus.
    /// </summary>
    public static float CalculateRank(float mean, float stdDev, int gamesPlayed, int gamesWon, int rankConvergence = 350, int rankLogBase = 55)
    {
        // k starts at 3.0 (very conservative) and approaches -1.0 (aggressive) as games increase
        // This allows rank to exceed mu for experienced high-winrate players
        var convergenceFactor = Math.Min(1.0f, (float)gamesPlayed / rankConvergence);
        var k = 3.0f - 4.0f * convergenceFactor; // 3.0 -> -1.0

        var baseRank = mean - k * stdDev;

        // Win rate bonus: scaled by log_base, kicks in as convergence increases
        var winRate = gamesPlayed > 0 ? (float)gamesWon / gamesPlayed : 0.5f;
        var wrBonus = (winRate - 0.5f) * rankLogBase * convergenceFactor * 0.5f;

        return Math.Max(1.0f, baseRank + wrBonus);
    }

    /// <summary>
    /// Determine bot difficulty level from skill mean using PvP thresholds from CMS.
    /// Default thresholds from matchmaking.json botlevelspvp:
    ///   Extreme(3) >= 35, Hard(2) >= 25, Normal(1) >= 10, Easy(0) >= 0
    /// </summary>
    public static int CalculateBotLevel(float skillMean, List<BotLevelThreshold>? thresholds = null)
    {
        thresholds ??= DefaultBotThresholds;

        foreach (var t in thresholds.OrderByDescending(t => t.SkillMin))
        {
            if (skillMean >= t.SkillMin)
                return t.BotLevel;
        }

        return 0;
    }

    private static readonly List<BotLevelThreshold> DefaultBotThresholds =
    [
        new() { BotLevel = 3, SkillMin = 35 },
        new() { BotLevel = 2, SkillMin = 25 },
        new() { BotLevel = 1, SkillMin = 10 },
        new() { BotLevel = 0, SkillMin = 0 }
    ];

    private static float Sigmoid(float x) => (float)(1.0 / (1.0 + Math.Exp(-1.7159 * x)));
}

public class BotLevelThreshold
{
    public int BotLevel { get; set; }
    public float SkillMin { get; set; }
}
