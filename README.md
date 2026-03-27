# PWAMessenger

A Blazor WebAssembly PWA with Firebase Cloud Messaging push notifications.

**Live client:** https://pwamessenger.pages.dev

## Stack

- **Frontend:** Blazor WebAssembly PWA (.NET 10), deployed to Cloudflare Pages
- **Backend:** ASP.NET Core Web API (.NET 10)
- **Database:** SQL Server
- **Push:** Firebase Cloud Messaging (FCM)

## Running locally

1. Run the database setup script: `Documents/setup-database.sql`
2. Add the Firebase service account path to API user secrets:
   ```json
   { "Firebase:CredentialPath": "C:\\path\\to\\adminsdk.json" }
   ```
3. Start both projects:
   ```bash
   dotnet run --project PWAMessenger.Api
   dotnet run --project PWAMessenger.Client
   ```

API runs at `https://localhost:7102`. Client runs at `https://localhost:7056`.

## Deployment

The client deploys automatically to Cloudflare Pages on push to `main` via GitHub Actions.
The API runs locally and is exposed over HTTPS via Visual Studio Dev Tunnels for cross-device access.

Update the `API_BASE_URL` GitHub Actions secret with the current Dev Tunnel URL before deploying.
