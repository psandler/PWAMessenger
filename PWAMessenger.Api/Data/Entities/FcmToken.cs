namespace PWAMessenger.Api.Data.Entities;

public class FcmToken
{
    public int TokenId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
