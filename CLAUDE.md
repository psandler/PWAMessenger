# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

This is a pre-implementation project. The only file currently present is the specification at `Documents/blazor-fcm-push-poc-spec.md`. All implementation must be created from scratch following that spec.

## Stack

- **Frontend:** Blazor WebAssembly PWA, .NET 10
- **Backend:** ASP.NET Core Web API, .NET 10
- **Database:** SQL Server (local instance or LocalDB)
- **Push:** Firebase Cloud Messaging (FCM) via `FirebaseAdmin` NuGet package
- **Tunneling:** Visual Studio Dev Tunnels (provides HTTPS for Push API and Service Workers)
- **FCM client:** Firebase JS SDK v9+ modular (not legacy compat)

## Scaffolding Commands

```bash
# Create the Blazor WASM PWA frontend
dotnet new blazorwasm --pwa -f net10.0 -n PWAMessenger.Client

# Create the ASP.NET Core API backend
dotnet new webapi -f net10.0 -n PWAMessenger.Api

# Create a solution and add projects
dotnet new sln -n PWAMessenger
dotnet sln add PWAMessenger.Client PWAMessenger.Api

# Add FirebaseAdmin to the API project
dotnet add PWAMessenger.Api package FirebaseAdmin
```

## Build & Run

```bash
dotnet build
dotnet run --project PWAMessenger.Api
dotnet run --project PWAMessenger.Client
```

## Architecture

```
Device A (Browser)         Local Machine              Device B (Browser)
[Blazor WASM PWA]  ──►  [ASP.NET Core API]
  send message UI           /api/users              [Blazor WASM PWA]
  JS FCM token reg          /api/tokens               service worker
                            /api/messages/send         push handler
                          [SQL Server]                OS notification
                            Users table
                            FcmTokens table
                          [FirebaseAdmin SDK] ──►  FCM (Google) ──► Device B
```

Dev Tunnels exposes the local server over HTTPS — both devices use the tunnel URL.

## Key Implementation Notes

### Database
- `Users` table: `UserId` (PK identity), `Username` (unique). Seed with 'Alice' and 'Bob'.
- `FcmTokens` table: `TokenId`, `UserId` (FK), `Token` (up to 500 chars), `RegisteredAt`, `LastSeenAt`. One user can have multiple tokens.
- Token registration is an **upsert**: update `LastSeenAt` if the userId+token pair exists, otherwise insert.

### Firebase / Backend
- Store the Firebase service account JSON file locally; reference its path in `appsettings.Development.json` under `Firebase:CredentialPath`. **Never commit the credential file.**
- Initialize in `Program.cs`:
  ```csharp
  FirebaseApp.Create(new AppOptions {
      Credential = GoogleCredential.FromFile(builder.Configuration["Firebase:CredentialPath"])
  });
  ```
- On send: fetch all tokens for `toUserId`, call `FirebaseMessaging.DefaultInstance.SendAsync()` per token, delete any tokens that FCM reports as invalid.

### Frontend JS Interop
Push API and FCM client SDK are JavaScript-only. Two JS files are needed, called from Blazor via `IJSRuntime`:

- **`wwwroot/push.js`** — imports Firebase modular SDK, exports `requestPermissionAndGetToken(vapidKey)`. Returns the FCM token string or null if permission denied. Called once on app startup after user selects identity.
- **`service-worker.published.js`** — push event handlers **must be merged into Blazor's existing service worker**, not a separate file. Blazor's PWA template owns this file.

### User Identity
No auth. On first load show a "Who are you?" dropdown populated from `GET /api/users`. Store selected `userId` in `localStorage`. Use it for all API calls and token registration.

## Critical Gotchas

- **HTTPS required:** Service Workers and Push API refuse to work on HTTP. Dev Tunnels handles this.
- **FCM token refresh:** Call `getToken()` on every app launch and re-POST to `/api/tokens`. Tokens can change without warning.
- **Service worker merge:** Push handlers go in `service-worker.published.js` alongside Blazor's offline cache logic — not in a separate file.
- **Firebase JS SDK version:** Use modular v9+ imports (`import { getMessaging } from "firebase/messaging"`), not the legacy compat layer.
- **iOS:** Notification permission requires the PWA to be added to the home screen first. Non-issue on Android/desktop.
- **Foreground suppression:** OS notifications are suppressed when the PWA is foregrounded — this is by design for the POC.

## Dev Tunnels Setup

In Visual Studio 2026: right-click project → Configure Dev Tunnel → Create Tunnel. Set persistence to **Persistent** (requires Microsoft account) and access to **Public**. Both test devices access the app via the generated tunnel URL.
