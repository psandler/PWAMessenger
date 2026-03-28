# Architecture

## Purpose

A lightweight invite app for paddle sports — tennis, pickleball, platform paddle, etc. Core workflows:

- **Create a match/event** — sport, time, location, number of players needed
- **Invite players** — invite registered users to a specific match (distinct from inviting someone to join the system)
- **Get responses** — invited players accept or decline; organizer sees who's in
- **Notifications** — all invitation and response activity delivers push notifications via FCM

The app is intentionally scoped. It is not a scheduling tool, a league manager, or a social network. It is the minimal thing needed to organize a game and get people on a court.

**This is also a learning project.** A primary goal is hands-on experience with event sourcing, event modeling, and vertical slice architecture using production-grade .NET tooling. It is unlikely to be a public production app.

---

## Identity & Authorization

### Principles
- Invite-only system. A user cannot authenticate until their phone number has been pre-registered in the system.
- Phone number is the user's identity. No usernames, email addresses, or other identifiers.
- Auth0 is the authentication provider (free tier, up to 7500 monthly active users).

### Login Flow
1. User opens the app and enters their phone number.
2. App checks the `InvitedUsers` table — if the number is not present, the user is rejected before Auth0 is contacted.
3. If the number is present, the app hands off to Auth0 SMS passwordless login.
4. Auth0 sends a one-time SMS code to the phone number.
5. User enters the code. Auth0 issues a JWT.
6. The JWT `sub` claim (format: `sms|+1XXXXXXXXXX`) becomes the user's permanent identity in the system.
7. On first login, the user is prompted to enter a display name. A `Users` record is then created. On subsequent logins, the FCM token is refreshed and the user goes directly to the app.

### InvitedUsers Table
Acts as a gatekeeper. A phone number must exist here before any Auth0 interaction is permitted. The invite and contacts flow that populates this table is defined separately.

### Display Name
Users enter a preferred display name during onboarding (first login). This is what other users see throughout the app. Stored in the `Users` table as `DisplayName`.

---

## Messaging

### Infrastructure
Firebase Cloud Messaging (FCM) handles all push notification delivery. This infrastructure is already implemented and is not changing.

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
| Send a direct message | `MessageSent` |
| Create a match | `MatchCreated` |
| Invite a player to a match | `PlayerInvited` |
| Accept an invitation | `InvitationAccepted` |
| Decline an invitation | `InvitationDeclined` |
| Cancel a match | `MatchCancelled` |
| Reschedule a match | `MatchRescheduled` |

Current state is derived by replaying events. Read models (projections) are built from the stream and stored in SQL Server tables managed by EF Core, then queried via EF Core.

#### A Note on MessageSent Evolution
`MessageSent` is intentionally minimal at first — direct user-to-user messages. It will likely need to evolve as the app grows (messages in the context of a match, group messages, etc.). Polecat supports event upcasting, which allows old event shapes to be transformed to new ones during replay without rewriting history. When `MessageSent` needs to change, the path is to introduce a new event type or an upcaster rather than modifying the existing event.

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

### EF Core — Read Model Layer
Entity Framework Core manages the relational read model tables (Users, InvitedUsers, FcmTokens) via code-first migrations. Polecat's async projection handlers inject `AppDbContext` to write projection state. All read queries go through EF Core.

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
  Auth0Id      NVARCHAR(100) NOT NULL UNIQUE   -- Auth0 sub claim (e.g. sms|+1XXXXXXXXXX)
  PhoneNumber  NVARCHAR(20)  NOT NULL UNIQUE   -- derived from Auth0Id for readability
  DisplayName  NVARCHAR(100) NOT NULL          -- user-chosen name entered during onboarding

-- Pre-registration gate (not event-sourced — plain relational data)
InvitedUsers
  InvitedUserId  INT PRIMARY KEY IDENTITY
  PhoneNumber    NVARCHAR(20) NOT NULL UNIQUE
  InvitedAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  InvitedBy      INT NULL REFERENCES Users(UserId)   -- null for seed/admin invites

-- Push notification tokens (EF-managed, upserted on each login)
FcmTokens
  TokenId       INT PRIMARY KEY IDENTITY
  UserId        INT NOT NULL REFERENCES Users(UserId)
  Token         NVARCHAR(500) NOT NULL
  RegisteredAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  LastSeenAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
```
