namespace PWAMessenger.Api.Features.RegisterUser;

public record UserRegistered(
    string Auth0Id,
    string PhoneNumber,
    string DisplayName,
    int InvitedUserId);
