using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PWAMessenger.Client;
using PWAMessenger.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Unauthenticated — used only for POST /api/login (pre-auth gate check)
builder.Services.AddHttpClient<LoginService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl));

// Authenticated — attaches JWT for all post-login API calls
builder.Services.AddHttpClient<ApiService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler(sp =>
    {
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
        handler.ConfigureHandler(authorizedUrls: [apiBaseUrl]);
        return handler;
    });

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
    options.ProviderOptions.ClientId = builder.Configuration["Auth0:ClientId"]!;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("email");
    // Route Auth0 to the email passwordless connection
    options.ProviderOptions.AdditionalProviderParameters["connection"] = "email";
    // Audience scopes the token to our API
    options.ProviderOptions.AdditionalProviderParameters["audience"] =
        builder.Configuration["Auth0:Audience"]!;
});

await builder.Build().RunAsync();
