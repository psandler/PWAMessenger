using System.Net.Http.Json;

namespace PWAMessenger.Client.Services;

// Uses the unauthenticated HTTP client — called before Auth0 login to check InvitedUsers.
public class LoginService(HttpClient http)
{
    public async Task<bool> CheckInvitedAsync(string email)
    {
        var response = await http.PostAsJsonAsync("api/login", new { Email = email });
        return response.IsSuccessStatusCode;
    }
}
