using Polecat;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.SendMessage;

public class SendMessageHandler(AppDbContext db, IDocumentSession session)
{
    public async Task<IResult> HandleAsync(int senderId, SendMessageCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Body))
            return Results.BadRequest("Message body is required.");

        var recipientExists = await db.Users.FindAsync([command.RecipientId], ct);
        if (recipientExists is null)
            return Results.NotFound("Recipient not found.");

        var messageId = Guid.NewGuid();
        var @event = new MessageSent(messageId, senderId, command.RecipientId, command.Body, DateTime.UtcNow);

        session.Events.Append(messageId, @event);
        await session.SaveChangesAsync(ct);

        return Results.Accepted();
    }
}
