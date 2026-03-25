# PWAMessenger

A proof-of-concept demonstrating OS-level push notifications between two browser/PWA instances using Blazor WebAssembly and Firebase Cloud Messaging.

User A sends a message to User B. Device B receives an OS notification even when the PWA is in the background.

**Live client:** https://psandler.github.io/PWAMessenger/

## Architecture

- **Frontend:** Blazor WebAssembly PWA, deployed to GitHub Pages
- **Backend:** ASP.NET Core Web API, runs locally and is exposed via Visual Studio Dev Tunnels
- **Database:** SQL Server (local)
- **Push:** Firebase Cloud Messaging

## Running locally

1. Run the database setup script: `Documents/setup-database.sql`
2. Add the Firebase service account path to API user secrets:
   ```json
   { "Firebase:CredentialPath": "C:\\path\\to\\adminsdk.json" }
   ```
3. Start the API:
   ```bash
   dotnet run --project PWAMessenger.Api
   ```
4. Start the client:
   ```bash
   dotnet run --project PWAMessenger.Client
   ```

The client runs at `https://localhost:7056` and calls the API at `https://localhost:7102`.

## Cross-device testing

Start a Dev Tunnel in Visual Studio (persistent + public), update the `API_BASE_URL`
GitHub Actions secret with the tunnel URL, then push to trigger a redeploy. Both
devices open the GitHub Pages URL.

## Docs

- `Documents/blazor-fcm-push-poc-spec.md` — full specification
- `Documents/setup-database.sql` — database schema and seed data
- `Documents/github-pages-deployment-plan.md` — deployment details
- `Documents/firebase-api-key-security.md` — notes on the Firebase API key
