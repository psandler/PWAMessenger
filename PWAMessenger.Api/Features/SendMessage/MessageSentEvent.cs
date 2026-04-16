namespace PWAMessenger.Api.Features.SendMessage;

public record MessageSent(
    Guid MessageId,
    int SenderId,
    int RecipientId,
    string Body,
    DateTime SentAt);
