# Slice 1 Model — Login and Exist

**Deliverable:** A real user can log in with their phone number, set a display name, and be reachable by push notification.

---

## Happy Path Narrative

User opened the app. They entered their phone number. They were redirected to Auth0 where they received a code via SMS and entered it, which authenticated them via a JWT. They were redirected back to the app where they were presented with an Onboarding screen, where they entered their Display Name. This made them a registered user. They were presented with an option of allowing notifications on their device, which they responded Allow. This made the system aware of how to send notifications to their device. Then they landed on the main page of the app, which for now just said "Hello, [DisplayName]".

---

## Events

| Event | Trigger | Stored in event stream |
|---|---|---|
| *(none)* | Phone number submitted — InvitedUsers gate check | No — query only |
| *(none)* | Auth0 OTP completed — JWT issued and received | No — Auth0's domain |
| `UserRegistered` | User submitted their display name | Yes |
| `FcmTokenRegistered` | Notification permission granted, device token obtained | Yes |

---

## Commands

| Command | Payload | Produces | Notes |
|---|---|---|---|
| `LoginCommand` | PhoneNumber | *(no event)* | Checks InvitedUsers; hands off to Auth0 if found, rejects if not. Auth0 handles everything after. |
| `RegisterUserCommand` | DisplayName | `UserRegistered` | Sent when user submits their display name on the Onboarding screen. Auth0Id comes from the JWT already in context. |
| `GrantNotificationPermissionCommand` | FcmToken | `FcmTokenRegistered` | Client requests permission, Firebase SDK returns the token, client sends both to the API as this command. |

---

## Read Models

| Read Model | Populated by | Consumed by | Notes |
|---|---|---|---|
| `InvitedUsers` | Seeded directly (admin) | `RegisterUserCommand` handler | Looked up by PhoneNumber to retrieve InvitedUserId. Not shown to the user — needed internally so UserRegistered carries the correlation. |
| `Users` | `UserRegistered` projection | App shell ("Hello, [DisplayName]"), future slices | The primary identity read model. |
| `FcmTokens` | `FcmTokenRegistered` projection | Future slices (sending notifications) | Holds device tokens per user. |

---

## Given / When / Then

**Test 1 — RegisterUserCommand**
- **Given:** InvitedUsers contains a record with a PhoneNumber matching the one encoded in Auth0Id
- **When:** `RegisterUserCommand(Auth0Id, DisplayName)`
- **Then:** `UserRegistered(Auth0Id, DisplayName, InvitedUserId)` is appended to the stream

**Test 2 — GrantNotificationPermissionCommand**
- **Given:** `UserRegistered` has been appended for this Auth0Id
- **When:** `GrantNotificationPermissionCommand(Auth0Id, FCMToken)`
- **Then:** `FcmTokenRegistered(Auth0Id, FCMToken)` is appended to the stream

**Test 3 — LoginCommand (happy path)**
- **Given:** InvitedUsers contains PhoneNumber
- **When:** `LoginCommand(PhoneNumber)`
- **Then:** Auth0 flow is initiated — no event appended

---

## Alternate Paths

**Rejected login — phone number not in InvitedUsers**
- **Given:** InvitedUsers does NOT contain PhoneNumber
- **When:** `LoginCommand(PhoneNumber)`
- **Then:** Request is rejected; Auth0 is never contacted; user sees an error

**Returning user — re-authentication (new session or new device)**
- Same as happy path through Auth0
- App receives JWT, detects Users record already exists for Auth0Id → skips onboarding
- If new device: `GrantNotificationPermissionCommand` fires → `FcmTokenRegistered` appended (additional token for this user)
- If same device: FCM token may be unchanged; upsert is a no-op
- No `UserRegistered` event — that only fires once, on first login

**Notification permission denied**
- `UserRegistered` fires normally
- `GrantNotificationPermissionCommand` is never sent — no event, no FcmTokens record created
- User lands on main page; app reads `Notification.permission` from the browser if it needs to show a "enable notifications" prompt
- Absence of FcmTokens record is the implicit signal; no explicit event needed
