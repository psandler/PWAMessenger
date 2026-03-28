using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.RegisterUser;

public class RegisterUserHandler(AppDbContext db /*, IDocumentSession session — uncomment when Polecat is installed */)
{
    public async Task<IResult> HandleAsync(string auth0Id, RegisterUserCommand command, CancellationToken ct = default)
    {
        var phoneNumber = PhoneNumberFromAuth0Id(auth0Id);

        if (await db.Users.AnyAsync(u => u.Auth0Id == auth0Id, ct))
            return Results.Conflict("User already registered.");

        var invited = await db.InvitedUsers
            .FirstOrDefaultAsync(i => i.PhoneNumber == phoneNumber, ct);

        if (invited is null)
            return Results.StatusCode(403);

        var @event = new UserRegistered(auth0Id, phoneNumber, command.DisplayName, invited.InvitedUserId);

        // TODO: Append to Polecat stream once package is installed
        // session.Events.Append(StreamId(auth0Id), @event);
        // await session.SaveChangesAsync(ct);

        await new UserRegisteredProjection().ProjectAsync(@event, db, ct);

        return Results.Ok();
    }

    // Auth0 sub claim format: sms|+1XXXXXXXXXX
    private static string PhoneNumberFromAuth0Id(string auth0Id)
    {
        var parts = auth0Id.Split('|');
        return parts.Length == 2 ? parts[1] : auth0Id;
    }

    // Deterministic stream ID derived from Auth0Id — used to correlate all events for a user.
    // Not for security purposes.
    internal static Guid StreamId(string auth0Id) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes(auth0Id)));
}
