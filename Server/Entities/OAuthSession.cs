using System.Security.Cryptography;

namespace OpenRocketArena.Server.Entities;

public class OAuthSession
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string AuthCode { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsConsumed { get; set; }

    public Account Account { get; set; } = null!;

    public static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").Replace("=", "");
}
