using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Features.RegisterUser;

namespace PWAMessenger.Api.Features.GrantNotificationPermission;

public class GrantNotificationPermissionHandler(AppDbContext db /*, IDocumentSession session — uncomment when Polecat is installed */)
{
    public async Task<IResult> HandleAsync(string auth0Id, GrantNotificationPermissionCommand command, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Auth0Id == auth0Id, ct);
        if (!userExists) return Results.NotFound("User not registered.");

        var @event = new FcmTokenRegistered(auth0Id, command.FcmToken);

        // TODO: Append to Polecat stream once package is installed
        // session.Events.Append(RegisterUserHandler.StreamId(auth0Id), @event);
        // await session.SaveChangesAsync(ct);

        await new FcmTokenRegisteredProjection().ProjectAsync(@event, db, ct);

        return Results.Ok();
    }
}
