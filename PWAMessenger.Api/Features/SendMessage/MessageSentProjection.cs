using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using JasperFx.Events;
using Microsoft.EntityFrameworkCore;
using Polecat;
using Polecat.EntityFrameworkCore;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Api.Features.SendMessage;

public class MessageSentProjection : EfCoreEventProjection<AppDbContext>
{
    public MessageSentProjection()
    {
        IncludeType<MessageSent>();
    }

    protected override async Task ProjectAsync(
        IEvent @event,
        AppDbContext db,
        IDocumentOperations operations,
        CancellationToken ct)
    {
        if (@event.Data is not MessageSent e) return;
        if (FirebaseApp.DefaultInstance is null) return;

        var sender = await db.Users.FindAsync([e.SenderId], ct);
        if (sender is null) return;

        var tokens = await db.FcmTokens
            .Where(t => t.UserId == e.RecipientId)
            .ToListAsync(ct);

        if (tokens.Count == 0) return;

        var messages = tokens
            .Select(t => new Message
            {
                Token = t.Token,
                Notification = new Notification
                {
                    Title = sender.DisplayName,
                    Body = e.Body
                },
                Data = new Dictionary<string, string> { ["url"] = "/" }
            })
            .ToList();

        var result = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages, ct);

        // Remove tokens FCM reports as no longer registered.
        var staleTokenIds = result.Responses
            .Select((r, i) => (r, token: tokens[i]))
            .Where(x => !x.r.IsSuccess &&
                        x.r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
            .Select(x => x.token.TokenId)
            .ToList();

        if (staleTokenIds.Count > 0)
        {
            var stale = await db.FcmTokens
                .Where(t => staleTokenIds.Contains(t.TokenId))
                .ToListAsync(ct);
            db.FcmTokens.RemoveRange(stale);
        }
        // Do NOT call SaveChangesAsync — Polecat commits atomically with event progression
    }
}
