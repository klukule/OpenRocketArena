using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;
using OpenRocketArena.Server.Entities;

namespace OpenRocketArena.Server.Services;

public record IamTokenInfo(long AccountId, long PersonaId, string PlayerId);

public class IamTokenStore(IServiceScopeFactory scopeFactory)
{
    public void Store(string accessToken, string refreshToken, IamTokenInfo info, int expiresInSeconds)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.IamSessions.Add(new IamSession
        {
            AccountId = info.AccountId == 0 ? null : info.AccountId,
            PersonaId = info.PersonaId,
            PlayerId = info.PlayerId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds)
        });
        db.SaveChanges();
    }

    public IamTokenInfo? Resolve(string accessToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = db.IamSessions.FirstOrDefault(s => s.AccessToken == accessToken && s.ExpiresAt > DateTime.UtcNow);
        return session != null ? new IamTokenInfo(session.AccountId ?? 0, session.PersonaId, session.PlayerId) : null;
    }

    public IamTokenInfo? ResolveByRefresh(string refreshToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = db.IamSessions.FirstOrDefault(s => s.RefreshToken == refreshToken);
        return session != null ? new IamTokenInfo(session.AccountId ?? 0, session.PersonaId, session.PlayerId) : null;
    }
}
