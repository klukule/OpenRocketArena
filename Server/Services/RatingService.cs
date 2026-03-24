namespace OpenRocketArena.Server.Services;

/// <summary>
/// Rating calculations for Open Rocket Arena. Uses TrueSkill algorithm
/// https://www.microsoft.com/en-us/research/wp-content/uploads/2007/01/NIPS2006_0688.pdf
/// </summary>
public static class RatingService
{
    // TrueSkill defaults
    public const double InitialMean = 25.0;
    public const double InitialStdDev = 8.0;            // Initial sigma
    private const double Beta = 25.0 / 6.0;             // ~4.167
    private const double DynamicsFactor = 25.0 / 300.0; // ~0.0833
    private const double DrawProbability = 0.0;

    public enum VictoryType { Win, Lose, Draw, Quit }

    /// <summary>
    /// Update a player's skill rating after a match.
    /// </summary>
    public static (double newMean, double newStdDev) UpdateRating(double mean, double stdDev, double opponentMean, double opponentStdDev, VictoryType outcome)
    {
        // If player quit do not update SR
        if (outcome == VictoryType.Quit)
            return (mean, stdDev);

        var drawMargin = GetDrawMarginFromDrawProbability(DrawProbability, Beta);
        var playerSigmaSq = stdDev * stdDev;
        var opponentSigmaSq = opponentStdDev * opponentStdDev;

        var c = Math.Sqrt(playerSigmaSq + opponentSigmaSq + 2.0 * Beta * Beta);

        var winningMean = mean;
        var losingMean = opponentMean;

        if (outcome == VictoryType.Lose)
        {
            winningMean = opponentMean;
            losingMean = mean;
        }

        var meanDelta = winningMean - losingMean;

        double v, w;
        var winLossMultiplier = 1.0;

        if (outcome == VictoryType.Draw)
        {
            v = VWithinMargin(meanDelta, drawMargin, c);
            w = WWithinMargin(meanDelta, drawMargin, c);
        }
        else
        {
            v = VExceedsMargin(meanDelta, drawMargin, c);
            w = WExceedsMargin(meanDelta, drawMargin, c);
            if (outcome == VictoryType.Lose)
                winLossMultiplier = -1.0;
        }

        // Update the mean
        var varianceWithDynamics = playerSigmaSq + DynamicsFactor * DynamicsFactor;
        var meanMultiplier = varianceWithDynamics / c;
        var newMean = mean + winLossMultiplier * meanMultiplier * v;

        // Update std dev
        var stdDevMultiplier = varianceWithDynamics / (c * c);
        var newStdDev = Math.Sqrt(varianceWithDynamics * (1.0 - w * stdDevMultiplier));

        // Fallback
        if (double.IsNaN(newStdDev) || double.IsInfinity(newStdDev))
            newStdDev = InitialStdDev;

        return (newMean, newStdDev);
    }

    /// <summary>
    /// Calculate display rank using:
    /// converganceAmount = min(gamesWon / convergenceValue, 1)
    /// x = converganceAmount * (logBase - 1) + 1
    /// rank = (mean + 5) * log_logBase(x) + 1
    /// Capped at 50.999999
    /// </summary>
    public static double CalculateRank(double mean, int gamesWon, int gamesQuit, int rankConvergence = 350, int rankLogBase = 55)
    {
        // Wins factor: penalize quits against wins, minimum 0
        var gamesWonFactor = (long)Math.Max(0, gamesWon - gamesQuit);

        var convergenceValue = (double)rankConvergence;
        var logBase = (double)rankLogBase;

        var converganceAmount = Math.Min(gamesWonFactor / convergenceValue, 1.0);
        var x = converganceAmount * (logBase - 1.0) + 1.0;

        var newRank = (mean + 5.0) * (Math.Log(x) / Math.Log(logBase)) + 1.0;
        if (newRank >= 50.999999)
            newRank = 50.999999;

        return newRank;
    }

    /// <summary>
    /// Determine bot difficulty level from skill mean using PvP thresholds from CMS.
    /// </summary>
    public static int CalculateBotLevel(double skillMean, List<BotLevelThreshold>? thresholds = null)
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

    // --- TrueSkill Gaussian helper functions ---

    private static double GetDrawMarginFromDrawProbability(double drawProbability, double beta)
    {
        var x = 0.5 * (drawProbability + 1.0);
        return GaussianSurvival(x) * Math.Sqrt(2.0) * beta;
    }

    /// <summary>
    /// V function for the case where the margin is exceeded (win/loss).
    /// v = N(t) / Phi(t) where t = (meanDelta - drawMargin) / c
    /// </summary>
    private static double VExceedsMargin(double meanDelta, double drawMargin, double c)
    {
        var t = (meanDelta - drawMargin) / c;
        var pdf = GaussianPdf(t);
        var cdf = GaussianCdf(t);
        return cdf < 1e-15 ? -t : pdf / cdf;
    }

    /// <summary>
    /// W function for the case where the margin is exceeded (win/loss).
    /// w = v * (v + t) where t = (meanDelta - drawMargin) / c
    /// </summary>
    private static double WExceedsMargin(double meanDelta, double drawMargin, double c)
    {
        var t = (meanDelta - drawMargin) / c;
        var vVal = VExceedsMargin(meanDelta, drawMargin, c);
        return vVal * (vVal + t);
    }

    /// <summary>
    /// V function for draws (within margin).
    /// </summary>
    private static double VWithinMargin(double meanDelta, double drawMargin, double c)
    {
        var tUpper = (drawMargin - meanDelta) / c;
        var tLower = (-drawMargin - meanDelta) / c;
        var pdfUpper = GaussianPdf(tUpper);
        var pdfLower = GaussianPdf(tLower);
        var cdfDiff = GaussianCdf(tUpper) - GaussianCdf(tLower);
        return cdfDiff < 1e-15 ? 0.0 : (pdfLower - pdfUpper) / cdfDiff;
    }

    /// <summary>
    /// W function for draws (within margin).
    /// </summary>
    private static double WWithinMargin(double meanDelta, double drawMargin, double c)
    {
        var tUpper = (drawMargin - meanDelta) / c;
        var tLower = (-drawMargin - meanDelta) / c;
        var pdfUpper = GaussianPdf(tUpper);
        var pdfLower = GaussianPdf(tLower);
        var cdfDiff = GaussianCdf(tUpper) - GaussianCdf(tLower);
        if (cdfDiff < 1e-15) return 1.0;

        var vVal = VWithinMargin(meanDelta, drawMargin, c);
        return vVal * vVal + (tUpper * pdfUpper - tLower * pdfLower) / cdfDiff;
    }

    // Standard normal PDF: N(x) = exp(-x^2/2) / sqrt(2*pi)
    private static double GaussianPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

    // Standard normal CDF: Phi(x) using the error function
    private static double GaussianCdf(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));

    // Survival function: 1 - Phi(x)
    private static double GaussianSurvival(double x) => 1.0 - GaussianCdf(x);

    // Approximation of the error function
    private static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        return sign * y;
    }
}

public class BotLevelThreshold
{
    public int BotLevel { get; set; }
    public float SkillMin { get; set; }
}
