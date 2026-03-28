namespace PWAMessenger.Api.Data.Entities;

public class InvitedUser
{
    public int InvitedUserId { get; set; }
    public string PhoneNumber { get; set; } = "";
    public DateTime InvitedAt { get; set; }
    public int? InvitedBy { get; set; }
    public User? InvitedByUser { get; set; }
}
