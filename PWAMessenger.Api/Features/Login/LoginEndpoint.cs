namespace PWAMessenger.Api.Features.Login;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/login", async (LoginCommand command, LoginHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(command, ct);
            return result == LoginResult.Proceed
                ? Results.Ok()
                : Results.StatusCode(403);
        });

        return app;
    }
}
