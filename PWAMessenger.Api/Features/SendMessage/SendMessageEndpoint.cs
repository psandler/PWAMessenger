using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.SendMessage;

public static class SendMessageEndpoint
{
    public static IEndpointRouteBuilder MapSendMessageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/messages/send", async (
            SendMessageCommand command,
            SendMessageHandler handler,
            HttpContext ctx,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var auth0Id = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (auth0Id is null) return Results.Unauthorized();

            var sender = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, ct);

            if (sender is null) return Results.Unauthorized();

            return await handler.HandleAsync(sender.UserId, command, ct);
        }).RequireAuthorization();

        return app;
    }
}
