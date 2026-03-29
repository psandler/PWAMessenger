using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using PWAMessenger.Api.Data;

namespace PWAMessenger.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SqlServer = "PHIL-POWERSPEC";
    private const string TestDatabase = "PWAMessengerTest";
    private const string TestConnectionString =
        $"Server={SqlServer};Database={TestDatabase};Trusted_Connection=True;TrustServerCertificate=True;";
    private const string MasterConnectionString =
        $"Server={SqlServer};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("test-signing-key-must-be-at-least-32-chars!!"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Auth0:Domain"] = "test.auth0.com",
                ["Auth0:Audience"] = "pwamessenger",
                // No Firebase:CredentialPath — skips Firebase init (guarded in Program.cs)
            });
        });

        builder.ConfigureServices(services =>
        {
            // Override JWT bearer to accept tokens signed with our test key.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    IssuerSigningKey = SigningKey,
                };
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Create the test database before the host starts so Polecat's activator
        // can connect when applying schema changes on startup.
        EnsureDatabaseExists();
        return base.CreateHost(builder);
    }

    private static void EnsureDatabaseExists()
    {
        using var conn = new SqlConnection(MasterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{TestDatabase}') CREATE DATABASE [{TestDatabase}]";
        cmd.ExecuteNonQuery();
    }

    // Generates a signed JWT with the given Auth0Id and email as claims.
    public string GenerateToken(string auth0Id, string email)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, auth0Id),
                new Claim("email", email),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    // Truncates application data between tests to ensure isolation.
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.FcmTokens.RemoveRange(db.FcmTokens);
        db.Users.RemoveRange(db.Users);
        db.InvitedUsers.RemoveRange(db.InvitedUsers);
        await db.SaveChangesAsync();
    }
}
