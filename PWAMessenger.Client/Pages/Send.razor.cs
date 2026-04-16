using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using PWAMessenger.Client.Models;

namespace PWAMessenger.Client.Pages;

public partial class Send
{
    List<UserSummary>? users;
    int selectedUserId;
    string body = "";
    bool sending;
    bool sent;
    string? error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            users = await Api.GetUsersAsync();
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
    }

    async Task SendAsync()
    {
        error = null;

        if (selectedUserId == 0)
        {
            error = "Please select a recipient.";
            return;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            error = "Please enter a message.";
            return;
        }

        sending = true;
        var success = await Api.SendMessageAsync(selectedUserId, body);
        sending = false;

        if (success)
            sent = true;
        else
            error = "Failed to send. Please try again.";
    }

    void Reset()
    {
        sent = false;
        body = "";
        selectedUserId = 0;
        error = null;
    }
}
