using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Polecat;
using Polecat.EntityFrameworkCore;
using PWAMessenger.Api.Data;
using PWAMessenger.Api.Features.GrantNotificationPermission;
using PWAMessenger.Api.Features.Login;
using PWAMessenger.Api.Features.RegisterUser;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddPolecat(opts =>
{
    opts.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
    opts.Projections.Add<FcmTokenRegisteredProjection, AppDbContext>(
        opts,
        new FcmTokenRegisteredProjection(),
        ProjectionLifecycle.Async);
})
.AddAsyncDaemon(DaemonMode.Solo)
.ApplyAllDatabaseChangesOnStartup();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidAudience = builder.Configuration["Auth0:Audience"],
            ValidIssuer = $"https://{builder.Configuration["Auth0:Domain"]}"
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<GrantNotificationPermissionHandler>();

using var credStream = File.OpenRead(builder.Configuration["Firebase:CredentialPath"]!);
FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromStream(credStream) });

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapLoginEndpoints();
app.MapRegisterUserEndpoints();
app.MapGrantNotificationPermissionEndpoints();

app.Run();
