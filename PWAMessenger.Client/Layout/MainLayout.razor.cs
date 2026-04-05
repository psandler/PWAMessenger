using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PWAMessenger.Client.Layout;

public partial class MainLayout
{
    void LogOut() => Navigation.NavigateToLogout("authentication/logout");
}
