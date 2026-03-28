using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Data.Entities;

namespace PWAMessenger.Api.Features.GrantNotificationPermission;

// TODO: Replace with Polecat async projection once the daemon is wired up.
// This projection is currently called inline from GrantNotificationPermissionHandler.
public class FcmTokenRegisteredProjection
{
    public async Task ProjectAsync(FcmTokenRegistered @event, AppDbContext db, CancellationToken ct = default)
    {
        var user = await db.Users.FirstAsync(u => u.Auth0Id == @event.Auth0Id, ct);

        var existing = await db.FcmTokens
            .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.Token == @event.FcmToken, ct);

        if (existing is not null)
        {
            existing.LastSeenAt = DateTime.UtcNow;
        }
        else
        {
            db.FcmTokens.Add(new FcmToken
            {
                UserId = user.UserId,
                Token = @event.FcmToken,
                RegisteredAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
