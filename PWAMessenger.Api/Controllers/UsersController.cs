using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using PWAMessenger.Api.Models;

namespace PWAMessenger.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IDbConnection db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<User>> GetAll() =>
        await db.QueryAsync<User>("SELECT UserId, Username FROM Users ORDER BY UserId");
}
