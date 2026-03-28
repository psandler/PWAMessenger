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

*To be completed.*
