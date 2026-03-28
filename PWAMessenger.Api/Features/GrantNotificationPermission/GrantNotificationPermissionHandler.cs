using Polecat;
using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Features.RegisterUser;

namespace PWAMessenger.Api.Features.GrantNotificationPermission;

public class GrantNotificationPermissionHandler(AppDbContext db, IDocumentSession session)
{
    public async Task<IResult> HandleAsync(string auth0Id, GrantNotificationPermissionCommand command, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Auth0Id == auth0Id, ct);
        if (!userExists) return Results.NotFound("User not registered.");

        var @event = new FcmTokenRegistered(auth0Id, command.FcmToken);

        session.Events.Append(RegisterUserHandler.StreamId(auth0Id), @event);
        await session.SaveChangesAsync(ct);

        await new FcmTokenRegisteredProjection().ProjectAsync(@event, db, ct);

        return Results.Ok();
    }
}
