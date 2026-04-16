using System.Net;
using System.Net.Http.Json;
using PWAMessenger.Client.Models;

namespace PWAMessenger.Client.Services;

public class ApiService(HttpClient http)
{
    // Returns null if the user is not yet registered (first login).
    public async Task<User?> GetCurrentUserAsync()
    {
        var response = await http.GetAsync("api/users/me");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<User>();
    }

    public async Task<bool> RegisterUserAsync(string displayName)
    {
        var response = await http.PostAsJsonAsync("api/users/register", new { DisplayName = displayName });
        return response.IsSuccessStatusCode;
    }

    public async Task RegisterFcmTokenAsync(string fcmToken)
    {
        await http.PostAsJsonAsync("api/notifications/register", new { FcmToken = fcmToken });
    }

    public async Task<List<UserSummary>> GetUsersAsync()
    {
        return await http.GetFromJsonAsync<List<UserSummary>>("api/users") ?? [];
    }

    public async Task<bool> SendMessageAsync(int recipientId, string body)
    {
        var response = await http.PostAsJsonAsync("api/messages/send", new { RecipientId = recipientId, Body = body });
        return response.IsSuccessStatusCode;
    }
}
