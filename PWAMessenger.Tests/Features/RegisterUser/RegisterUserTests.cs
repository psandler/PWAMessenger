using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Data.Entities;
using PWAMessenger.Tests.Infrastructure;

namespace PWAMessenger.Tests.Features.RegisterUser;

[Collection("Integration")]
public class RegisterUserTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TestAuth0Id = "email|test-user-001";
    private const string TestEmail = "user@example.com";

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InvitedUser_CanRegister_And_UserRowIsCreated()
    {
        // Given
        await SeedInvitedUserAsync(TestEmail);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(TestAuth0Id, TestEmail));

        // When
        var response = await _client.PostAsJsonAsync("api/users/register", new { DisplayName = "Test User" });

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.SingleOrDefault(u => u.Auth0Id == TestAuth0Id);
        Assert.NotNull(user);
        Assert.Equal(TestEmail, user.Email);
        Assert.Equal("Test User", user.DisplayName);
    }

    [Fact]
    public async Task AlreadyRegistered_Returns409()
    {
        // Given
        await SeedInvitedUserAsync(TestEmail);
        await SeedUserAsync(TestAuth0Id, TestEmail, "Existing User");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(TestAuth0Id, TestEmail));

        // When
        var response = await _client.PostAsJsonAsync("api/users/register", new { DisplayName = "Test User" });

        // Then
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_AfterRegistration_ReturnsUser()
    {
        // Given
        await SeedInvitedUserAsync(TestEmail);
        await SeedUserAsync(TestAuth0Id, TestEmail, "Test User");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(TestAuth0Id, TestEmail));

        // When
        var response = await _client.GetAsync("api/users/me");

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal("Test User", body.DisplayName);
        Assert.Equal(TestEmail, body.Email);
    }

    [Fact]
    public async Task GetCurrentUser_BeforeRegistration_Returns404()
    {
        // Given — user is authenticated but has no Users row
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(TestAuth0Id, TestEmail));

        // When
        var response = await _client.GetAsync("api/users/me");

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedInvitedUserAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.InvitedUsers.Add(new InvitedUser { Email = email, InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task SeedUserAsync(string auth0Id, string email, string displayName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User { Auth0Id = auth0Id, Email = email, DisplayName = displayName });
        await db.SaveChangesAsync();
    }

    private record UserResponse(string DisplayName, string Email);
}
