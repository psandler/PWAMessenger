namespace PWAMessenger.Client.Models;

public record User(int UserId, string Auth0Id, string Email, string DisplayName);
