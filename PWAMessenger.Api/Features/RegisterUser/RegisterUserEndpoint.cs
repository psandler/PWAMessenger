using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.RegisterUser;

public static class RegisterUserEndpoint
{
    public static IEndpointRouteBuilder MapRegisterUserEndpoints(this IEndpointRouteBuilder app)
    {
        // Called after Auth0 login to complete first-time onboarding.
        app.MapPost("/api/users/register", async (
            RegisterUserCommand command,
            RegisterUserHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var auth0Id = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = ctx.User.FindFirst(ClaimTypes.Email)?.Value
                     ?? ctx.User.FindFirst("email")?.Value;

            if (auth0Id is null || email is null) return Results.Unauthorized();

            return await handler.HandleAsync(auth0Id, email, command, ct);
        }).RequireAuthorization();

        // Called after Auth0 login to determine first-time vs. returning user.
        // 200 + user → returning user, skip onboarding.
        // 404 → first-time user, show onboarding screen.
        app.MapGet("/api/users/me", async (
            HttpContext ctx,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var auth0Id = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (auth0Id is null) return Results.Unauthorized();

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, ct);

            return user is null ? Results.NotFound() : Results.Ok(user);
        }).RequireAuthorization();

        return app;
    }
}
