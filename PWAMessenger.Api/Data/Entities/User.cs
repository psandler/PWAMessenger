namespace PWAMessenger.Api.Data.Entities;

public class User
{
    public int UserId { get; set; }
    public string Auth0Id { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
