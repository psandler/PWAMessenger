using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.GetUsers;

// SCAFFOLD — temporary endpoint for Slice 2.
// Returns all registered users except the caller.
// Replace with proper contact/invite discovery in a future slice.
public static class GetUsersEndpoint
{
    public static IEndpointRouteBuilder MapGetUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", async (
            HttpContext ctx,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var auth0Id = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (auth0Id is null) return Results.Unauthorized();

            var users = await db.Users
                .AsNoTracking()
                .Where(u => u.Auth0Id != auth0Id)
                .Select(u => new { u.UserId, u.DisplayName })
                .ToListAsync(ct);

            return Results.Ok(users);
        }).RequireAuthorization();

        return app;
    }
}
