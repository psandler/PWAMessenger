namespace PWAMessenger.Api.Features.RegisterUser;

public record UserRegistered(
    string Auth0Id,
    string Email,
    string DisplayName,
    int InvitedUserId);
