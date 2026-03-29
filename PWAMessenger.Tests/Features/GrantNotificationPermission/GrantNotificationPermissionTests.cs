using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Data.Entities;
using PWAMessenger.Tests.Infrastructure;

namespace PWAMessenger.Tests.Features.GrantNotificationPermission;

[Collection("Integration")]
public class GrantNotificationPermissionTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TestAuth0Id = "email|test-user-002";
    private const string TestEmail = "notify@example.com";
    private const string TestFcmToken = "fcm-token-abc123";

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteredUser_CanRegisterFcmToken()
    {
        // Given
        await SeedUserAsync(TestAuth0Id, TestEmail, "Notify User");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(TestAuth0Id, TestEmail));

        // When
        var response = await _client.PostAsJsonAsync(
            "api/notifications/register",
            new { FcmToken = TestFcmToken });

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnregisteredUser_Returns404()
    {
        // Given — authenticated but no Users row
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(TestAuth0Id, TestEmail));

        // When
        var response = await _client.PostAsJsonAsync(
            "api/notifications/register",
            new { FcmToken = TestFcmToken });

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedUserAsync(string auth0Id, string email, string displayName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.InvitedUsers.Add(new InvitedUser { Email = email, InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        db.Users.Add(new User { Auth0Id = auth0Id, Email = email, DisplayName = displayName });
        await db.SaveChangesAsync();
    }
}
