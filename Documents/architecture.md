# Architecture

## Purpose

A lightweight invite app for racket sports — tennis, pickleball, platform tennis, etc. Core workflows:

- **Create a match/event** — sport, time, location, number of players needed
- **Invite players** — invite registered users to a specific match (distinct from inviting someone to join the system)
- **Get responses** — invited players accept or decline; organizer sees who's in
- **Notifications** — all invitation and response activity delivers push notifications via FCM

The app is intentionally scoped. It is not a scheduling tool, a league manager, or a social network. It is the minimal thing needed to organize a game and get people on a court.

**This is also a learning project.** A primary goal is hands-on experience with event sourcing, event modeling, and vertical slice architecture using production-grade .NET tooling. It is unlikely to be a public production app.

---

## Identity & Authorization

### Principles
- Invite-only system. A user cannot authenticate until their email address has been pre-registered in the system.
- Email address is the user's identity. No usernames, phone numbers, or other identifiers.
- Auth0 is the authentication provider (free tier, up to 7,500 monthly active users).

### Login Flow
1. User opens the app and enters their email address.
2. App checks the `InvitedUsers` table — if the email is not present, the user is rejected before Auth0 is contacted.
3. If the email is present, the app hands off to Auth0 email passwordless login.
4. Auth0 sends a magic link or one-time code to the email address. Auth0 handles delivery natively — no external email provider required.
5. User clicks the link or enters the code. Auth0 issues a JWT.
6. The JWT `sub` claim becomes the user's permanent Auth0Id in the system. The `email` claim carries the email address.
7. On first login, the user is prompted to enter a display name. A `Users` record is then created.
8. The user is then prompted to allow notifications. If granted, the browser issues an FCM token which is stored in `FcmTokens`. The user may skip this step.
9. On subsequent logins, the user goes directly to the app. The FCM token is refreshed in the background if notification permission was previously granted.

### InvitedUsers Table
Acts as a gatekeeper. An email address must exist here before any Auth0 interaction is permitted. The invite and contacts flow that populates this table is defined separately.

### Display Name
Users enter a preferred display name during onboarding (first login). This is what other users see throughout the app. Stored in the `Users` table as `DisplayName`.

---

## Messaging

### Infrastructure
Firebase Cloud Messaging (FCM) handles all push notification delivery. This infrastructure is already implemented.

### Flow
1. On login, the client calls `getToken()` (Firebase JS SDK) to obtain an FCM registration token.
2. The token is registered in the `FcmTokens` table, linked to the authenticated user's `Auth0Id`.
3. To send a message, the sender's client POSTs to `/api/messages/send` with the recipient's identity and message text.
4. The API looks up all FCM tokens for the recipient and calls `FirebaseMessaging.DefaultInstance.SendAsync()` for each.
5. Invalid/expired tokens are deleted automatically on send failure.

### Behavior Notes
- One user can have multiple FCM tokens (multiple devices/browsers).
- OS notifications are suppressed when the PWA is in the foreground (by design — FCM behavior).
- Notification permission on iOS requires the PWA to be installed to the home screen first.

---

## Technical Approach

### Vertical Slice Architecture
Code is organized by feature (slice) rather than by layer. Each slice — `CreateMatch`, `InviteToMatch`, `RespondToInvitation`, etc. — contains everything it needs: command/query definition, handler, projections, and any API endpoint. No shared service layers. Slices communicate only through the event stream, not by calling each other directly.

Reference: [Vertical Slice Architecture — Jimmy Bogard](https://www.jimmybogard.com/vertical-slice-architecture/)

### Event Sourcing
State is never updated in place. Everything that happens is recorded as an immutable event appended to a stream. Examples for this domain:

| Command | Event |
|---|---|
| Invite user to system | `UserInvited` |
| User registers (first login) | `UserRegistered` |
| Device notification token stored | `FcmTokenRegistered` |
| Send a direct message | `MessageSent` |
| Create a match | `MatchCreated` |
| Invite a player to a match | `PlayerInvited` |
| Accept an invitation | `InvitationAccepted` |
| Decline an invitation | `InvitationDeclined` |
| Cancel a match | `MatchCancelled` |
| Reschedule a match | `MatchRescheduled` |

Current state is derived by replaying events. Read models (projections) are built from the stream and stored in SQL Server tables managed by EF Core, then queried via EF Core.

#### Event Evolution
Events are immutable once written, but their shape will need to evolve as the app grows. Polecat supports event upcasting, which allows old event shapes to be transformed to new ones during replay without rewriting history. When an event needs to change, the path is to introduce a new event type or an upcaster rather than modifying the existing event definition.

### Event Modeling
The design methodology used to plan the system before writing code. Events, commands, and read models are laid out on a timeline — what triggers what, what each user sees at each step. This is done as a design artifact before any slice is implemented.

**No code is written for a slice until its model is agreed on.**

#### Principles applied here

- **Information flow** — every field in a command must be traceable to a read model that displayed it to the user. If the user can't see a piece of data, they can't act on it.
- **Completeness** — every event must be consumed by at least one projection or automation. Every read model must be populated by at least one event. No orphaned projections; no unhandled events.
- **Testability** — Given/When/Then test cases fall directly out of the model. Given this read model state, when this command is submitted, then these events are appended.
- **Gap detection** — working through the model surfaces missing events, missing read models, and broken flows before any code is written.

Reference: [Event Modeling — Adam Dymitruk](https://eventmodeling.org/)

### Polecat
The event store and document database, running on SQL Server. Polecat is JasperFx's SQL Server port of Marten, using SQL Server 2025's native JSON type. Key capabilities used here:

- **Event store** — append events to streams keyed by aggregate ID (e.g. match ID)
- **Async projections** — the Polecat daemon rebuilds read models as events arrive; also the trigger point for sending FCM notifications
- Registration: `builder.Services.AddPolecat(options => options.Connection("..."))`

Reference: [Polecat — polecat.netlify.app](https://polecat.netlify.app/)
GitHub: [github.com/JasperFx/polecat](https://github.com/JasperFx/polecat)

### Inline vs Async Projections

Not all projections belong in the async daemon. The rule:

**Inline projections** — called synchronously within the command handler, before `SaveChangesAsync`. The event write and the projection write succeed or fail together. Use when:
- The read model participates in write-time validation (e.g. unique constraint on `DisplayName` — a duplicate should fail the command)
- The read model is read immediately after the write (e.g. `GET /api/users/me` called right after registration)
- Idempotency checks depend on the projection being current

Example: `UserRegistered → Users table` — inline, because the `Users` row is used to check "already registered" on the next call, and a unique constraint failure should abort the write.

**Async daemon projections** — processed by the Polecat daemon after the event is committed. Eventual consistency is acceptable. Use when:
- The projection is a query-only read model not needed during the write
- The projection is a side effect (FCM notification send)
- A projection failure should not roll back the command

Example: `FcmTokenRegistered → FcmTokens table` — async, because the FCM token just needs to be eventually available for future notification sends; a delay or failure does not affect the command result.

**Package:** async projections that write to EF Core tables use `Polecat.EntityFrameworkCore` and extend `EfCoreEventProjection<TDbContext>`. Registered via `opts.Projections.Add<TProjection, TDbContext>(opts, instance, ProjectionLifecycle.Async)` with `.AddAsyncDaemon(DaemonMode.Solo)` chained on `AddPolecat`.

### EF Core — Read Model Layer
Entity Framework Core manages the relational read model tables (Users, InvitedUsers, FcmTokens) via code-first migrations. Polecat's async projection handlers inject `AppDbContext` to write projection state. All read queries go through EF Core.

> Projecting events onto a relational schema (rather than a document store) is not the default pattern in most event sourcing frameworks, but we have chosen it here and Polecat's `Polecat.EntityFrameworkCore` package supports it as a first-class option.

The two layers are complementary:
- **Polecat** owns event streams (its own internal tables, Polecat-managed schema)
- **EF Core** owns the relational tables that serve as read models and pre-auth data

### FCM as an Event Side Effect
Push notifications are not triggered by direct API calls between slices. Instead, a Polecat async projection listens for specific events (`PlayerInvited`, `InvitationAccepted`, etc.) and fires the FCM send as a side effect. The existing FCM infrastructure (token registration, send logic, service worker) is unchanged — it just gets a new trigger mechanism.

---

## Implementation Plan

See [implementation-plan.md](implementation-plan.md).

---

## Database Schema

Two separate layers share the same SQL Server database:

**Polecat-managed tables** (auto-created, do not touch):
- Event streams, event data, projection daemon state — all managed internally by Polecat.

**EF Core-managed tables** (code-first migrations in `PWAMessenger.Api`):

```sql
-- Authenticated users (EF read model, written by UserRegistered projection)
Users
  UserId       INT PRIMARY KEY IDENTITY
  Auth0Id      NVARCHAR(100) NOT NULL UNIQUE   -- Auth0 sub claim
  Email        NVARCHAR(256) NOT NULL UNIQUE   -- from Auth0 email claim
  DisplayName  NVARCHAR(100) NOT NULL          -- user-chosen name entered during onboarding

-- Pre-registration gate (not event-sourced — plain relational data)
InvitedUsers
  InvitedUserId  INT PRIMARY KEY IDENTITY
  Email          NVARCHAR(256) NOT NULL UNIQUE
  InvitedAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  InvitedBy      INT NULL REFERENCES Users(UserId)   -- null for seed/admin invites

-- Push notification tokens (EF read model, written by FcmTokenRegistered projection)
FcmTokens
  TokenId       INT PRIMARY KEY IDENTITY
  UserId        INT NOT NULL REFERENCES Users(UserId)
  Token         NVARCHAR(500) NOT NULL
  RegisteredAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  LastSeenAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
```
