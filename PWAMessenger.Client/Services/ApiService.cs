using System.Net.Http.Json;
using PWAMessenger.Client.Models;

namespace PWAMessenger.Client.Services;

public class ApiService(HttpClient http)
{
    public Task<List<User>?> GetUsersAsync() =>
        http.GetFromJsonAsync<List<User>>("api/users");

    public async Task RegisterTokenAsync(int userId, string token)
    {
        var response = await http.PostAsJsonAsync("api/tokens", new { userId, token });
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> SendMessageAsync(int fromUserId, int toUserId, string messageText)
    {
        var response = await http.PostAsJsonAsync("api/messages/send",
            new { fromUserId, toUserId, messageText });
        return response.IsSuccessStatusCode;
    }
}
