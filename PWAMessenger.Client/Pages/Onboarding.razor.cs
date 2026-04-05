using Microsoft.JSInterop;

namespace PWAMessenger.Client.Pages;

public partial class Onboarding
{
    string displayName = "";
    bool loading;
    bool awaitingNotification;
    string? error;
    IJSObjectReference? pushModule;

    protected override async Task OnInitializedAsync()
    {
        pushModule = await JS.InvokeAsync<IJSObjectReference>("import", "./push.js");
    }

    async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        loading = true;
        var success = await Api.RegisterUserAsync(displayName);
        loading = false;
        if (!success)
        {
            error = "Registration failed. Please try again.";
            return;
        }
        awaitingNotification = true;
    }

    async Task RequestNotificationsAsync()
    {
        var vapidKey = Config["Firebase:VapidKey"]!;
        var token = await pushModule!.InvokeAsync<string?>("requestPermissionAndGetToken", vapidKey);
        if (token is not null)
            await Api.RegisterFcmTokenAsync(token);
        Navigation.NavigateTo("/", replace: true);
    }

    void SkipNotifications() => Navigation.NavigateTo("/", replace: true);
}
