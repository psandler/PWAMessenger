using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Data.Entities;
using PWAMessenger.Tests.Infrastructure;

namespace PWAMessenger.Tests.Features.Login;

[Collection("Integration")]
public class LoginTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InvitedEmail_Returns200()
    {
        // Given
        await SeedInvitedUserAsync("invited@example.com");

        // When
        var response = await _client.PostAsJsonAsync("api/login", new { Email = "invited@example.com" });

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEmail_Returns403()
    {
        // Given — no seed, email is not in InvitedUsers

        // When
        var response = await _client.PostAsJsonAsync("api/login", new { Email = "unknown@example.com" });

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SeedInvitedUserAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.InvitedUsers.Add(new InvitedUser { Email = email, InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}
