# PWAMessenger

An invite-only racket sports messaging app. Users log in with their email address, set a display name, and receive push notifications when invited to matches or sent messages.

**Live client:** https://pwamessenger.pages.dev

## Stack

- **Frontend:** Blazor WebAssembly PWA (.NET 10), deployed to Cloudflare Pages
- **Backend:** ASP.NET Core Web API (.NET 10)
- **Auth:** Auth0 email passwordless (OTP)
- **Database:** SQL Server
- **Event store:** Polecat 1.4.0 (JasperFx)
- **Read models:** EF Core 10.0.5, code-first migrations
- **Push:** Firebase Cloud Messaging (FCM)

## Architecture

Vertical slice architecture with event sourcing. Each feature slice owns its command, handler, projections, and endpoint. State changes are recorded as immutable events via Polecat; read models are projections onto EF Core-managed SQL Server tables.

See `Documents/architecture.md` for full design detail.

## Running locally

### Prerequisites

- SQL Server (local instance)
- Auth0 tenant configured (see `Documents/human-todo.md`)
- Firebase service account JSON (see `Documents/human-todo.md`)

### User Secrets

`PWAMessenger.Api`:
```json
{
  "ConnectionStrings:DefaultConnection": "Server=...;Database=PWAMessenger;Trusted_Connection=True;TrustServerCertificate=True;",
  "Auth0:Domain": "your-tenant.auth0.com",
  "Auth0:Audience": "pwamessenger",
  "Firebase:CredentialPath": "C:\\path\\to\\adminsdk.json"
}
```

`PWAMessenger.Client` (`appsettings.Development.json`):
```json
{
  "Auth0:Domain": "your-tenant.auth0.com",
  "Auth0:ClientId": "your-client-id",
  "Auth0:Audience": "pwamessenger",
  "Firebase:VapidKey": "your-vapid-key"
}
```

### Run

```bash
dotnet build
dotnet ef database update --project PWAMessenger.Api
dotnet run --project PWAMessenger.Api       # https://localhost:7102
dotnet run --project PWAMessenger.Client    # https://localhost:7056
```

## Tests

Integration tests use a real SQL Server test database (`PWAMessengerTest`), created and torn down automatically.

```bash
dotnet test PWAMessenger.Tests
```

## Deployment

The client deploys automatically to Cloudflare Pages on push to `main` via `.github/workflows/deploy-client.yml`.

The API runs locally and is exposed over HTTPS via Visual Studio Dev Tunnels. Update the `API_BASE_URL` GitHub Actions secret with the current tunnel URL before deploying.
