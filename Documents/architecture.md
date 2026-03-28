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

Current state is derived by replaying events. Read models (projections) are built from the stream and stored for query performance.

#### A Note on MessageSent Evolution
`MessageSent` is intentionally minimal at first — direct user-to-user messages. It will likely need to evolve as the app grows (messages in the context of a match, group messages, etc.). Marten supports event upcasting, which allows old event shapes to be transformed to new ones during replay without rewriting history. When `MessageSent` needs to change, the path is to introduce a new event type or an upcaster rather than modifying the existing event.

### Event Modeling
The design methodology used to plan the system before writing code. Events, commands, and read models are laid out on a timeline — what triggers what, what each user sees at each step. This is done as a design artifact before any slice is implemented.

Reference: [Event Modeling — Adam Dymitruk](https://eventmodeling.org/)

### Marten
The event store and document database. Marten runs on PostgreSQL (primary target) or SQL Server via Polecat (newer, in active development). Key capabilities used here:

- **Event store** — append events to streams keyed by aggregate ID (e.g. match ID)
- **Async projections** — the Marten daemon rebuilds read models as events arrive; also the trigger point for sending FCM notifications
- **Document store** — for read models and any data that doesn't need to be event-sourced (user profiles, etc.)

Reference: [Marten — martendb.io](https://martendb.io/)
Polecat (SQL Server support): [github.com/JasperFx/polecat](https://github.com/JasperFx/polecat)

### FCM as an Event Side Effect
Push notifications are not triggered by direct API calls between slices. Instead, a Marten async projection listens for specific events (`PlayerInvited`, `InvitationAccepted`, etc.) and fires the FCM send as a side effect. The existing FCM infrastructure (token registration, send logic, service worker) is unchanged — it just gets a new trigger mechanism.

---

## Implementation Plan

The goal is working software from the earliest possible point. The approach is a two-slice walking skeleton before any match domain work begins.

### Slice 1 — Login and Exist
Everything needed for a user to authenticate and be reachable.

- Seed `InvitedUsers` directly in the database (your phone number, any test devices). This is not a hack — it is the legitimate initial state of an invite-only system, and these seed entries will remain as admin/owner accounts.
- Auth0 SMS passwordless login
- Pre-auth gate: check `InvitedUsers` before handing off to Auth0
- `UserRegistered` event → creates the Users read model record with display name
- FCM token registered on login via existing infrastructure

**Deliverable:** A real user can log in with their phone number, set a display name, and be reachable by push notification.

### Slice 2 — Send a Message
Everything needed for one authenticated user to send a push notification to another.

- Temporary "all registered users except me" query endpoint — a scaffold, clearly marked, to be replaced by the contacts/invite discovery flow
- `MessageSent` event sourced via Marten
- Marten async projection handles the FCM send as a side effect
- Minimal send UI (no match context yet)

**Deliverable:** Full end-to-end — authenticated user sends a message, recipient receives an OS push notification. Functionally equivalent to the POC but with real identity and event sourcing.

### After the Walking Skeleton
Match creation and the full invite/contacts flow follow once the above is solid. The temporary "all registered users" endpoint is removed when proper recipient discovery is in place.

---

## Database Schema (Current)

```sql
-- Authenticated users
Users
  UserId       INT PRIMARY KEY IDENTITY
  Auth0Id      NVARCHAR(100) NOT NULL UNIQUE   -- Auth0 sub claim (e.g. sms|+1XXXXXXXXXX)
  PhoneNumber  NVARCHAR(20)  NOT NULL UNIQUE   -- derived from Auth0Id for readability
  DisplayName  NVARCHAR(100) NOT NULL          -- user-chosen name entered during onboarding

-- Pre-registration gate
InvitedUsers
  InvitedUserId  INT PRIMARY KEY IDENTITY
  PhoneNumber    NVARCHAR(20) NOT NULL UNIQUE
  InvitedAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  InvitedBy      INT NULL REFERENCES Users(UserId)   -- null for seed/admin invites

-- Push notification tokens
FcmTokens
  TokenId       INT PRIMARY KEY IDENTITY
  UserId        INT NOT NULL REFERENCES Users(UserId)
  Token         NVARCHAR(500) NOT NULL
  RegisteredAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  LastSeenAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
```

_Note: The current codebase has a simpler `Users` table (UserId, Username) seeded with placeholder data. This schema reflects the target state once Auth0 integration is implemented._
