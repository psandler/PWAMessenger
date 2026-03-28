namespace PWAMessenger.Api.Data.Entities;

public class User
{
    public int UserId { get; set; }
    public string Auth0Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
