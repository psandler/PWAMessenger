using System.Data;
using Dapper;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Mvc;
using PWAMessenger.Api.Models;
using PWAMessenger.Api.Requests;

namespace PWAMessenger.Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController(IDbConnection db) : ControllerBase
{
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendMessageRequest request)
    {
        var sender = await db.QuerySingleOrDefaultAsync<User>(
            "SELECT UserId, Username FROM Users WHERE UserId = @UserId",
            new { UserId = request.FromUserId });

        if (sender is null)
            return BadRequest("Unknown sender");

        var tokens = (await db.QueryAsync<string>(
            "SELECT Token FROM FcmTokens WHERE UserId = @UserId",
            new { UserId = request.ToUserId })).ToList();

        if (tokens.Count == 0)
            return Ok(new { sent = 0, stale = 0 });

        var staleTokens = new List<string>();

        foreach (var token in tokens)
        {
            var message = new Message
            {
                Notification = new Notification
                {
                    Title = $"Message from {sender.Username}",
                    Body = request.MessageText
                },
                Data = new Dictionary<string, string>
                {
                    ["fromUserId"] = request.FromUserId.ToString(),
                    ["url"] = "/"
                },
                Token = token
            };

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
            }
            catch (FirebaseMessagingException ex)
                when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered
                                            or MessagingErrorCode.InvalidArgument)
            {
                staleTokens.Add(token);
            }
        }

        if (staleTokens.Count > 0)
        {
            await db.ExecuteAsync(
                "DELETE FROM FcmTokens WHERE Token IN @Tokens",
                new { Tokens = staleTokens });
        }

        return Ok(new { sent = tokens.Count - staleTokens.Count, stale = staleTokens.Count });
    }
}
