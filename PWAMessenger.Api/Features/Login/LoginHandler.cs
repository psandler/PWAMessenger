using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.Login;

public class LoginHandler(AppDbContext db)
{
    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var exists = await db.InvitedUsers
            .AnyAsync(u => u.Email == command.Email, ct);

        return exists ? LoginResult.Proceed : LoginResult.Rejected;
    }
}

public enum LoginResult { Proceed, Rejected }
