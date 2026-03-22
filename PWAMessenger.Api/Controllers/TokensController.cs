using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using PWAMessenger.Api.Requests;

namespace PWAMessenger.Api.Controllers;

[ApiController]
[Route("api/tokens")]
public class TokensController(IDbConnection db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterTokenRequest request)
    {
        const string sql = """
            MERGE FcmTokens AS target
            USING (SELECT @UserId AS UserId, @Token AS Token) AS source
                ON target.UserId = source.UserId AND target.Token = source.Token
            WHEN MATCHED THEN
                UPDATE SET LastSeenAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, Token, RegisteredAt, LastSeenAt)
                VALUES (@UserId, @Token, GETUTCDATE(), GETUTCDATE());
            """;

        await db.ExecuteAsync(sql, new { request.UserId, request.Token });
        return Ok();
    }
}
