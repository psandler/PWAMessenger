using System.Data;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddScoped<IDbConnection>(_ =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

using var credStream = File.OpenRead(builder.Configuration["Firebase:CredentialPath"]!);
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromStream(credStream)
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
