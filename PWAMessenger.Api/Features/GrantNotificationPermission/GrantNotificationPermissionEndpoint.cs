using System.Security.Claims;

namespace PWAMessenger.Api.Features.GrantNotificationPermission;

public static class GrantNotificationPermissionEndpoint
{
    public static IEndpointRouteBuilder MapGrantNotificationPermissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/notifications/register", async (
            GrantNotificationPermissionCommand command,
            GrantNotificationPermissionHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var auth0Id = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (auth0Id is null) return Results.Unauthorized();

            return await handler.HandleAsync(auth0Id, command, ct);
        }).RequireAuthorization();

        return app;
    }
}
