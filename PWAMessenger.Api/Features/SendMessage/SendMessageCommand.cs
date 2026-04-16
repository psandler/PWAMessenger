namespace PWAMessenger.Api.Features.SendMessage;

public record SendMessageCommand(int RecipientId, string Body);
