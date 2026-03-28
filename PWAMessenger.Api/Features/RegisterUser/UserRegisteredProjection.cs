using PWAMessenger.Api.Data;
using PWAMessenger.Api.Data.Entities;

namespace PWAMessenger.Api.Features.RegisterUser;

// TODO: Replace with Polecat async projection once the daemon is wired up.
// This projection is currently called inline from RegisterUserHandler.
public class UserRegisteredProjection
{
    public async Task ProjectAsync(UserRegistered @event, AppDbContext db, CancellationToken ct = default)
    {
        db.Users.Add(new User
        {
            Auth0Id = @event.Auth0Id,
            PhoneNumber = @event.PhoneNumber,
            DisplayName = @event.DisplayName
        });

        await db.SaveChangesAsync(ct);
    }
}
