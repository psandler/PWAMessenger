# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git Operations

**NO GIT ACTIONS BY AGENTS.** Only humans perform git operations (commit, push, branch, merge, tag, PR creation).

- Never attempt automated commits or repository modifications

## Stack

- **Frontend:** Blazor WebAssembly PWA, .NET 10 (`PWAMessenger.Client`)
- **Backend:** ASP.NET Core Web API, .NET 10 (`PWAMessenger.Api`)
- **Auth:** Auth0, email passwordless (OTP). Email is the user's identity.
- **Database:** SQL Server
- **Event store:** Polecat 1.4.0 — JasperFx's SQL Server port of Marten
- **Read models:** EF Core 10.0.5, code-first migrations, relational schema
- **Push:** Firebase Cloud Messaging (FCM) via `FirebaseAdmin` NuGet package
- **FCM client:** Firebase JS SDK v9+ modular (not legacy compat)

## Architectural Patterns

- **Vertical slice architecture** — code organized by feature, not by layer. Each slice owns its command/query, handler, projections, and endpoint.
- **Event sourcing** — all state changes are recorded as immutable events via Polecat. Read models are built from projections onto EF Core-managed relational tables.
- **Inline vs async projections** — see section below.
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
  └─ push.js              Firebase modular SDK, getToken() via importmap
  └─ service-worker.js    Firebase compat SDK (importScripts), push + notificationclick handlers
  └─ Auth0                Email passwordless OTP, JWT issued on login

ASP.NET Core API (vertical slices)
  └─ Auth0 JWT validation middleware
  └─ Polecat IDocumentSession per slice (event append + SaveChangesAsync)
  └─ EF Core AppDbContext per slice (read model queries + inline projections)
  └─ Polecat async daemon — EfCoreEventProjection<AppDbContext> handlers

SQL Server
  └─ Polecat tables: pc_events, pc_streams, pc_event_progression (auto-created)
  └─ EF Core tables: Users, InvitedUsers, FcmTokens (code-first migrations)
```

## Polecat — Key Implementation Details

Polecat is new (released March 2026) and documentation is sparse. Use these verified patterns:

### Namespaces
```csharp
using Polecat;                          // IDocumentSession, IDocumentStore, StoreOptions
using JasperFx;                         // AutoCreate
using JasperFx.Events;                  // IEvent (used in projection ProjectAsync signature)
using JasperFx.Events.Daemon;           // DaemonMode
using JasperFx.Events.Projections;     // ProjectionLifecycle
using Polecat.EntityFrameworkCore;      // EfCoreEventProjection<T>, extension methods
```

### Registration (Program.cs)
```csharp
builder.Services.AddPolecat(opts =>
{
    opts.ConnectionString = connectionString;          // property setter, not opts.Connection()
    opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
    opts.Projections.Add<MyProjection, AppDbContext>(
        opts,
        new MyProjection(),
        ProjectionLifecycle.Async);
})
.AddAsyncDaemon(DaemonMode.Solo)           // Single-node; use HotCold for multi-node
.ApplyAllDatabaseChangesOnStartup();       // Creates pc_* tables before first request
```

### Async Projection Pattern
```csharp
// Package: Polecat.EntityFrameworkCore 1.4.0
public class MyProjection : EfCoreEventProjection<AppDbContext>
{
    public MyProjection()
    {
        IncludeType<MyEvent>();   // required — tells daemon which events to route here
    }

    protected override async Task ProjectAsync(
        IEvent @event,
        AppDbContext db,
        IDocumentOperations operations,
        CancellationToken ct)
    {
        if (@event.Data is not MyEvent e) return;
        // write to db here
        // DO NOT call db.SaveChangesAsync() — Polecat calls it atomically
    }
}
```

### Inline Projection Pattern (when projection participates in write transaction)
```csharp
// Called directly from handler before returning — no base class needed
public class MyInlineProjection
{
    public async Task ProjectAsync(MyEvent @event, AppDbContext db, CancellationToken ct)
    {
        // write to db
        await db.SaveChangesAsync(ct);   // called explicitly here
    }
}
// In handler: await new MyInlineProjection().ProjectAsync(@event, db, ct);
```

### Inline vs Async — Which to Use
- **Inline** — read model participates in write-time validation (unique constraint, idempotency check) OR is read immediately after the write. Example: `UserRegistered → Users`.
- **Async** — query-only read model or side effect that can be eventually consistent. Example: `FcmTokenRegistered → FcmTokens`.

### Event Append
```csharp
session.Events.Append(streamId, @event);   // streamId is a Guid
await session.SaveChangesAsync(ct);
// Deterministic stream ID from Auth0Id:
// new Guid(MD5.HashData(Encoding.UTF8.GetBytes(auth0Id)))
```

## Key Implementation Notes

### Auth0
- Email passwordless — user enters email on our login page, we gate-check against `InvitedUsers`, then redirect to Auth0 with `login_hint`.
- Auth0 tenant must have: **Identifier First** authentication profile, email passwordless connection enabled for the app, API with audience `pwamessenger`.
- JWT `sub` claim is the user's permanent Auth0Id. The `email` claim requires an Auth0 Action to inject it into the access token (it's not there by default).
- Auth0 credentials go in **VS User Secrets** (`Auth0:Domain`, `Auth0:Audience`). Never committed.

### Firebase / Backend
- Service account JSON path goes in **VS User Secrets** under `Firebase:CredentialPath`. Never committed.
- FCM sends will be triggered by Polecat async projections reacting to events (not yet implemented).
- Invalid/expired tokens are deleted when FCM reports them as unregistered.

### Frontend JS
- **`wwwroot/push.js`** — ES module, imported via `IJSRuntime`. Uses Firebase importmap in `index.html`. Passes Blazor's registered service worker to `getToken()` so Firebase doesn't look for a separate `firebase-messaging-sw.js`.
- **`service-worker.published.js`** — push handlers merged into Blazor's existing service worker using `importScripts` with Firebase compat CDN. `appsettings.json` is excluded from the offline cache (injected post-publish by the deploy workflow).

### Deployment
- Client deploys to Cloudflare Pages via GitHub Actions (`.github/workflows/deploy-client.yml`).
- Workflow injects `API_BASE_URL` secret into `wwwroot/appsettings.json` post-publish and creates a stable `dotnet.js` alias (required because .NET 10 fingerprints runtime files but `blazor.webassembly.js` imports the stable name).
- API runs locally behind Visual Studio Dev Tunnels (HTTPS required for Push API and Service Workers); update the `API_BASE_URL` GitHub secret when the tunnel URL changes.

## Critical Gotchas

- **HTTPS required:** Service Workers and Push API refuse to work on HTTP.
- **FCM token refresh:** Call `getToken()` on every app launch and re-POST to `/api/notifications/register`. Tokens can change without warning.
- **Service worker merge:** Push handlers use Firebase compat (`importScripts`) not ES modules — ES modules are unreliable in service worker scope.
- **iOS:** Notification permission requires the PWA to be added to the home screen first.
- **Foreground suppression:** OS notifications are suppressed when the PWA is in the foreground.
- **SRI hashes:** Any file modified after `dotnet publish` must be excluded from the Blazor offline cache in `offlineAssetsExclude`, otherwise the service worker fails to install.
- **Auth0 email in access token:** Must add a Post Login Action to inject `email` claim — it is not included in access tokens by default.
- **Polecat `ApplyAllDatabaseChangesOnStartup`** — without this, pc_* tables are created lazily on first use, which can cause errors if the first request hits the event store before it's ready.
