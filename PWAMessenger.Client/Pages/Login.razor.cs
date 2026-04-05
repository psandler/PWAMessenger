using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PWAMessenger.Client.Pages;

public partial class Login
{
    string email = "";
    string? error;
    bool loading;

    async Task HandleLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        loading = true;
        error = null;

        var invited = await LoginSvc.CheckInvitedAsync(email);
        if (!invited)
        {
            error = "This email address has not been invited to the system.";
            loading = false;
            return;
        }

        var options = new InteractiveRequestOptions
        {
            Interaction = InteractionType.SignIn,
            ReturnUrl = "/"
        };
        options.TryAddAdditionalParameter("login_hint", email);
        Navigation.NavigateToLogin("authentication/login", options);
    }
}
