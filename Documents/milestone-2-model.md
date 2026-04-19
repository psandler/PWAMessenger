# Milestone 2 Model — Send a Direct Message

**Deliverable:** A registered user can select another registered user and send them a text message. The recipient receives an OS push notification.

> **Note:** This model was written after the milestone was coded — a process violation. Future slices must have an agreed model before any code is written. See CLAUDE.md and `Documents/milestone-1-model.md` for the established format.

---

## Happy Path Narrative

User is logged in and on the home page. They tap "Send" in the nav. They see a list of all other registered users. They select a recipient, type a message, and tap Send. The app immediately shows "Sent!". In the background, the API appends a `MessageSent` event. The Polecat async daemon picks up the event, looks up the recipient's FCM tokens, and sends a push notification via Firebase. The recipient's device displays an OS notification showing the sender's display name and the message body.

---

## Events

| Event | Trigger | Stored in event stream |
|---|---|---|
| `MessageSent` | Sender submits the send form | Yes |

---

## Commands

| Command | Payload | Produces | Notes |
|---|---|---|---|
| `SendMessageCommand` | RecipientId, Body | `MessageSent` | API resolves SenderId from the JWT. Returns 202 Accepted immediately — FCM send is async. |

---

## Read Models

| Read Model | Populated by | Consumed by | Notes |
|---|---|---|---|
| `Users` | `UserRegistered` (Milestone 1) | `GET /api/users` — recipient list | Already exists. `GetUsers` endpoint is a scaffold: returns all registered users except the caller. No new projection needed. |
| `FcmTokens` | `FcmTokenRegistered` (Milestone 1) | `MessageSentProjection` | Queried by RecipientId to find target devices. Stale tokens deleted when FCM returns `Unregistered`. |

---

## Automations (Async Side Effects)

| Trigger Event | Action | Notes |
|---|---|---|
| `MessageSent` | Send FCM push notification to all recipient FCM tokens | Implemented in `MessageSentProjection`. If `FirebaseApp.DefaultInstance` is null (e.g. local dev without credentials), the projection is a no-op. |

---

## Given / When / Then

**Test 1 — SendMessageCommand (happy path)**
- **Given:** Sender (UserId=1) and recipient (UserId=2) are registered; recipient has at least one FCM token
- **When:** `SendMessageCommand(RecipientId=2, Body="Are you free Saturday?")`
- **Then:** `MessageSent(MessageId, SenderId=1, RecipientId=2, Body="Are you free Saturday?", SentAt)` is appended; FCM notification is sent to recipient's tokens

**Test 2 — SendMessageCommand (recipient has no tokens)**
- **Given:** Recipient has no FCM tokens (never granted notification permission)
- **When:** `SendMessageCommand(RecipientId=2, Body="...")`
- **Then:** `MessageSent` is still appended; no FCM call is made; no error

**Test 3 — SendMessageCommand (stale FCM token)**
- **Given:** Recipient has one FCM token that FCM reports as `Unregistered`
- **When:** `MessageSent` projection runs
- **Then:** FCM send attempted; `Unregistered` error received; token deleted from `FcmTokens`

---

## Alternate Paths

**Sender not registered**
- JWT is valid but no `Users` row exists for this Auth0Id (e.g. bypassed onboarding)
- `POST /api/messages/send` returns 401 — sender lookup fails

**Recipient not found**
- `RecipientId` does not exist in `Users`
- Handler returns 404 — no event appended

**Empty message body**
- Handler returns 400 — no event appended

**Firebase credentials not configured (local dev)**
- `FirebaseApp.DefaultInstance` is null
- `MessageSentProjection` exits early — event is still persisted, notification is silently skipped

---

## Scaffold Notes

`GET /api/users` is a temporary endpoint. It returns all registered users with no concept of contacts, mutual connections, or match context. It must be replaced when the invite/contacts flow is implemented. It is clearly marked as a scaffold in the code.
