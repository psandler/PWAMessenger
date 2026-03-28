namespace PWAMessenger.Api.Features.GrantNotificationPermission;

public record FcmTokenRegistered(string Auth0Id, string FcmToken);
