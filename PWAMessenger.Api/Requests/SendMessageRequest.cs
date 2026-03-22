namespace PWAMessenger.Api.Requests;

public record SendMessageRequest(int FromUserId, int ToUserId, string MessageText);
