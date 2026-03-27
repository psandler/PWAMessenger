# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Stack

- **Frontend:** Blazor WebAssembly PWA, .NET 10 (`PWAMessenger.Client`)
- **Backend:** ASP.NET Core Web API, .NET 10 (`PWAMessenger.Api`)
- **Database:** SQL Server — connection string in `PWAMessenger.Api/appsettings.json`
- **Push:** Firebase Cloud Messaging (FCM) via `FirebaseAdmin` NuGet package
- **Tunneling:** Visual Studio Dev Tunnels (HTTPS required for Push API and Service Workers)
- **FCM client:** Firebase JS SDK v9+ modular (not legacy compat)

## Build & Run

```bash
dotnet build
dotnet run --project PWAMessenger.Api       # https://localhost:7102
dotnet run --project PWAMessenger.Client    # https://localhost:7056
```

## Architecture

```
Browser (Blazor WASM PWA)
  └─ push.js           Firebase modular SDK, getToken() via importmap
  └─ service-worker.js Firebase compat SDK (importScripts), push + notificationclick handlers

ASP.NET Core API
  └─ GET  /api/users
  └─ POST /api/tokens          upsert FCM token per user
  └─ POST /api/messages/send   send FCM push to all tokens for target user

SQL Server
  └─ Users (UserId, Username)
  └─ FcmTokens (TokenId, UserId, Token, RegisteredAt, LastSeenAt)
```

## Key Implementation Notes

### Firebase / Backend
- Service account JSON path goes in **VS User Secrets** under `Firebase:CredentialPath`. Never committed.
- On send: fetch all tokens for `toUserId`, call `FirebaseMessaging.DefaultInstance.SendAsync()` per token, delete tokens FCM reports as invalid.
- Data access uses Dapper + `IDbConnection` (no EF).

### Frontend JS
- **`wwwroot/push.js`** — ES module, imported via `IJSRuntime`. Uses Firebase importmap in `index.html`. Passes Blazor's registered service worker to `getToken()` so Firebase doesn't look for a separate `firebase-messaging-sw.js`.
- **`service-worker.published.js`** — push handlers merged into Blazor's existing service worker using `importScripts` with Firebase compat CDN. `appsettings.json` is excluded from the offline cache (injected post-publish by the deploy workflow).

### Deployment
- Client deploys to Cloudflare Pages via GitHub Actions (`.github/workflows/deploy-client.yml`).
- Workflow injects `API_BASE_URL` secret into `wwwroot/appsettings.json` post-publish and creates a stable `dotnet.js` alias (required because .NET 10 fingerprints runtime files but `blazor.webassembly.js` imports the stable name).
- API runs locally behind Dev Tunnels; update the `API_BASE_URL` GitHub secret when the tunnel URL changes.

## Critical Gotchas

- **HTTPS required:** Service Workers and Push API refuse to work on HTTP.
- **FCM token refresh:** Call `getToken()` on every app launch and re-POST to `/api/tokens`. Tokens can change without warning.
- **Service worker merge:** Push handlers use Firebase compat (`importScripts`) not ES modules — ES modules are unreliable in service worker scope.
- **iOS:** Notification permission requires the PWA to be added to the home screen first.
- **Foreground suppression:** OS notifications are suppressed when the PWA is in the foreground.
- **SRI hashes:** Any file modified after `dotnet publish` must be excluded from the Blazor offline cache in `offlineAssetsExclude`, otherwise the service worker fails to install.
