# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git Operations

**NO GIT ACTIONS BY AGENTS.** Only humans perform git operations (commit, push, branch, merge, tag, PR creation).

- Never attempt automated commits or repository modifications

## Stack

- **Frontend:** Blazor WebAssembly PWA, .NET 10 (`PWAMessenger.Client`)
- **Backend:** ASP.NET Core Web API, .NET 10 (`PWAMessenger.Api`)
- **Auth:** Auth0, SMS passwordless (phone number is the user's identity)
- **Database:** PostgreSQL via Marten (primary target); SQL Server via Polecat under consideration
- **Event store:** Marten — event sourcing + document store + async projections
- **Push:** Firebase Cloud Messaging (FCM) via `FirebaseAdmin` NuGet package
- **Tunneling:** Visual Studio Dev Tunnels (HTTPS required for Push API and Service Workers)
- **FCM client:** Firebase JS SDK v9+ modular (not legacy compat)

## Architectural Patterns

- **Vertical slice architecture** — code organized by feature, not by layer. Each slice owns its command/query, handler, projections, and endpoint.
- **Event sourcing** — all state changes are recorded as immutable events via Marten. Read models are built from projections. FCM notifications fire as async projection side effects, not direct API calls.
- See `Documents/architecture.md` for full design detail, event catalog, and implementation plan.

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
  └─ Auth0             SMS passwordless, JWT issued on login

ASP.NET Core API (vertical slices)
  └─ Auth0 JWT validation middleware
  └─ Marten IDocumentSession / IEventStore per slice
  └─ Async projection daemon — triggers FCM sends on events

PostgreSQL (via Marten)
  └─ Event streams (UserRegistered, MessageSent, MatchCreated, ...)
  └─ Document projections (Users read model, FcmTokens, ...)
  └─ InvitedUsers (pre-auth gate, plain table)
```

_Note: The codebase currently reflects the POC state (SQL Server, Dapper, no auth). The above is the target architecture being built toward._

## Key Implementation Notes

### Auth0
- SMS passwordless — user enters phone number, receives OTP, Auth0 issues JWT.
- Pre-auth gate: check `InvitedUsers` table before initiating Auth0 flow.
- JWT `sub` claim (`sms|+1XXXXXXXXXX`) is the user's permanent identity across the system.
- Auth0 credentials go in **VS User Secrets**. Never committed.

### Firebase / Backend
- Service account JSON path goes in **VS User Secrets** under `Firebase:CredentialPath`. Never committed.
- FCM sends are triggered by Marten async projections reacting to events, not by direct API calls.
- Invalid/expired tokens are deleted when FCM reports them as unregistered.

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
