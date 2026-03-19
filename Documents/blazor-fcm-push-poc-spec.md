# POC Spec: Cross-Device Push Notifications with Blazor WASM + FCM

## Overview

A minimal proof-of-concept demonstrating OS-level push notifications between two browser/PWA instances. User A, on Device A, sends a text message to User B. Device B receives an OS-level push notification even if the PWA is not in the foreground.

## Constraints & Scope

- Two users, two devices. No multi-user generalization required.
- Local backend (ASP.NET Core + SQL Server) exposed via Visual Studio Dev Tunnels over HTTPS.
- No authentication for the POC — users identify themselves by picking a username from a seeded list (e.g., "Alice" and "Bob").
- No message history UI required, though the send UI needs a minimal form.
- Success criterion: Device B receives an OS-level notification when User A sends a message, with the app in the background.

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (PWA template) |
| Backend API | ASP.NET Core Web API (.NET 10) |
| Database | SQL Server (local instance or LocalDB) |
| Push delivery | Firebase Cloud Messaging (FCM) — direct, no Azure Notification Hubs |
| HTTPS tunnel | Visual Studio Dev Tunnels |
| FCM .NET SDK | `FirebaseAdmin` NuGet package (Google's official SDK) |

---

## Architecture

```
Device A (Browser)          Your Local Machine              Device B (Browser)
                         ┌─────────────────────┐
[Blazor WASM PWA]  ───►  │  ASP.NET Core API   │
  - Send message UI       │  - /api/messages     │
  - JS: FCM token reg     │  - /api/users        │          [Blazor WASM PWA]
                          │                     │            - Service Worker
                          │  SQL Server (local) │◄───────    - Push handler
                          │  - Users table      │            - OS notification
                          │  - FcmTokens table  │
                          │                     │
                          │  FirebaseAdmin SDK  │──────►  FCM (Google)
                          └─────────────────────┘              │
                                                               ▼
                                                        Device B browser
                                                        (OS notification)
```

Dev Tunnels provides the public HTTPS URL that both devices use to reach the local API and serve the Blazor WASM app.

---

## Database Schema

### `Users`
```sql
CREATE TABLE Users (
    UserId      INT PRIMARY KEY IDENTITY,
    Username    NVARCHAR(50) NOT NULL UNIQUE
);

-- Seed data
INSERT INTO Users (Username) VALUES ('Phil'), ('Maggie');
```

### `FcmTokens`
```sql
CREATE TABLE FcmTokens (
    TokenId      INT PRIMARY KEY IDENTITY,
    UserId       INT NOT NULL REFERENCES Users(UserId),
    Token        NVARCHAR(500) NOT NULL,
    RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastSeenAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

One user can have multiple tokens (multiple browsers/devices). The backend sends to all tokens registered for the target user.

---

## API Endpoints

### `GET /api/users`
Returns the list of users. Used to populate the "Send to" dropdown on the send form.

**Response:**
```json
[
  { "userId": 1, "username": "Alice" },
  { "userId": 2, "username": "Bob" }
]
```

### `POST /api/tokens`
Called by the Blazor app on startup (via JS Interop) to register or refresh the FCM token for the current user.

**Request:**
```json
{
  "userId": 1,
  "token": "<fcm-registration-token>"
}
```

**Behavior:** Upsert — if a token already exists for this userId+token combination, update `LastSeenAt`. If new, insert. This handles the FCM requirement to refresh tokens on each visit.

### `POST /api/messages/send`
Triggers a push notification to the target user.

**Request:**
```json
{
  "fromUserId": 1,
  "toUserId": 2,
  "messageText": "Hey, are you there?"
}
```

**Behavior:**
1. Look up all FCM tokens for `toUserId`
2. For each token, call `FirebaseMessaging.DefaultInstance.SendAsync()`
3. Handle invalid token responses from FCM — delete stale tokens from the DB
4. Return 200 OK

---

## Backend Implementation Notes

### Firebase Setup
- Create a Firebase project (free tier) at console.firebase.google.com
- Enable Cloud Messaging
- Generate a service account private key (JSON file)
- Store the JSON file locally; reference it in `appsettings.Development.json` as a file path
- Do **not** commit the credential JSON to source control

### FirebaseAdmin Initialization
```csharp
// Program.cs
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(
        builder.Configuration["Firebase:CredentialPath"])
});
```

### Send Logic
```csharp
var message = new Message
{
    Notification = new Notification
    {
        Title = $"Message from {senderUsername}",
        Body = messageText
    },
    Data = new Dictionary<string, string>
    {
        ["fromUserId"] = fromUserId.ToString(),
        ["url"] = "/messages"
    },
    Token = fcmToken
};

await FirebaseMessaging.DefaultInstance.SendAsync(message);
```

---

## Frontend Implementation Notes

### PWA Template
Use the Blazor WASM PWA project template targeting .NET 10:
```
dotnet new blazorwasm --pwa -f net10.0
```
This generates the required `manifest.json` and `service-worker.js` scaffolding.

### JS Interop Boundary
The Push API and FCM client SDK are JavaScript-only. Two JS functions are needed, called from Blazor via `IJSRuntime`.

**`wwwroot/push.js`**
```javascript
import { initializeApp } from "firebase/app";
import { getMessaging, getToken } from "firebase/messaging";

const firebaseConfig = { /* your Firebase project config */ };
const app = initializeApp(firebaseConfig);
const messaging = getMessaging(app);

export async function requestPermissionAndGetToken(vapidKey) {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') return null;
    return await getToken(messaging, { vapidKey });
}
```

This function is called once on app startup after the user selects their identity. The returned token is then POSTed to `/api/tokens`.

### Service Worker Push Handler
Add to `service-worker.published.js` (must be merged into Blazor's existing service worker, not a separate file):

```javascript
self.addEventListener('push', event => {
    const payload = event.data.json();
    event.waitUntil(
        self.registration.showNotification(payload.notification.title, {
            body: payload.notification.body,
            icon: '/icon-192.png',
            badge: '/badge-72.png',
            data: { url: payload.data?.url }
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    event.waitUntil(
        clients.openWindow(event.notification.data.url || '/')
    );
});
```

### User Selection / Identity
On first load, the app presents a simple "Who are you?" screen — a dropdown populated from `GET /api/users`. The selected `userId` is stored in `localStorage` and used for all subsequent API calls and token registration. No passwords, no JWT, no Auth0.

### Send Message UI
A simple form with:
- "To:" dropdown (all users except the current user, from `GET /api/users`)
- Message text input
- Send button → calls `POST /api/messages/send`

---

## Dev Tunnels Setup

1. In Visual Studio 2026, right-click the project → Configure Dev Tunnel → Create Tunnel
2. Set persistence to **Persistent** so the URL doesn't change between sessions (requires a Microsoft account)
3. Set access to **Public** so Device B can reach it without authentication prompts
4. Both test devices access the app at the tunnel URL (e.g., `https://xyz-5000.usw2.devtunnels.ms`)
5. Ensure the app's API base URL config references the tunnel URL

---

## FCM Gotchas to Anticipate

- **Token refresh:** Call `getToken()` on every app launch and re-register with the backend. FCM tokens can change without warning.
- **iOS requires PWA install:** On iPhone, the user must add the PWA to the home screen before notification permission can even be requested. Non-issue if both test devices are Android or desktop browsers.
- **Blazor's service worker conflict:** The default Blazor PWA service worker intercepts all fetches for offline support. Push event handlers must be merged into `service-worker.published.js`, not added as a separate file.
- **HTTPS is non-negotiable:** Service workers and the Push API will not work on HTTP. Dev Tunnels handles this automatically.
- **Firebase JS SDK version:** Use the modular v9+ SDK (`import { getMessaging } from "firebase/messaging"`), not the legacy compat SDK.
- **Foreground suppression:** When the PWA is in the foreground, the browser typically suppresses OS notifications. See the Future Extensions section for how to handle this.

---

## Future Extensions (Out of Scope for POC)

### Deep Linking
The `notificationclick` handler already includes a `url` field and calls `clients.openWindow()` — this is the foundation of deep linking. To extend:
- Include a specific route in the notification payload (e.g., `/messages/thread/42`)
- In the click handler, use `clients.matchAll()` to check if the PWA is already open; if so, focus it and post a message to trigger Blazor router navigation rather than opening a second window
- Blazor's router handles the route normally — no special deep link infrastructure needed

### In-App Notifications (No SignalR Required)
Two viable approaches when the PWA is in the foreground:

**Polling:** The app calls `GET /api/messages/unread` on a timer (e.g., every 5-10 seconds) and updates an in-app badge or toast on results. Simple, no new infrastructure, latency up to the poll interval.

**Service Worker postMessage:** The service worker checks `clients.matchAll()` on push receipt. If a focused client exists, it skips `showNotification()` and instead posts the payload directly to the Blazor page via `postMessage()`. The page listens via JS Interop and updates the UI. This gives OS notification when backgrounded and in-app notification when foregrounded — from the same push event, no SignalR needed. FCM delivery latency (typically seconds) is the only tradeoff vs. SignalR.

### Production Additions
- Auth0 authentication (free tier covers up to 7,500 users)
- Azure SignalR Service for sub-second in-app delivery if polling latency is unacceptable
- Azure Notification Hubs if expanding to native iOS/Android apps
- Azure hosting (App Service + Azure SQL) to replace local + Dev Tunnels
- Token cleanup job to remove stale FCM tokens

---

## What This POC Deliberately Omits

- Authentication or authorization of any kind
- Message persistence or history
- Read receipts or badge count management
- Multiple devices per user
- Robust error handling (basic FCM failure logging only)
- Token cleanup automation
