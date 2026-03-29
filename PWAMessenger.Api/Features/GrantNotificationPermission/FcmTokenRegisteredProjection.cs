using JasperFx.Events;
using Microsoft.EntityFrameworkCore;
using Polecat;
using Polecat.EntityFrameworkCore;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Data.Entities;

namespace PWAMessenger.Api.Features.GrantNotificationPermission;

public class FcmTokenRegisteredProjection : EfCoreEventProjection<AppDbContext>
{
    public FcmTokenRegisteredProjection()
    {
        IncludeType<FcmTokenRegistered>();
    }

    protected override async Task ProjectAsync(
        IEvent @event,
        AppDbContext db,
        IDocumentOperations operations,
        CancellationToken ct)
    {
        if (@event.Data is not FcmTokenRegistered e) return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0Id == e.Auth0Id, ct);
        if (user is null) return;

        var existing = await db.FcmTokens
            .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.Token == e.FcmToken, ct);

        if (existing is not null)
        {
            existing.LastSeenAt = DateTime.UtcNow;
        }
        else
        {
            db.FcmTokens.Add(new FcmToken
            {
                UserId = user.UserId,
                Token = e.FcmToken,
                RegisteredAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            });
        }
        // Do NOT call SaveChangesAsync — Polecat commits atomically with event progression
    }
}
