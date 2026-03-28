# Human Setup Checklist

Tasks that require human action before or alongside implementation. Work through these roughly in order.

---

## 1. Auth0 Setup

Auth0 provides SMS passwordless authentication. This requires a real Twilio account for production SMS delivery.

### 1a. Create Auth0 Tenant
1. Go to [auth0.com](https://auth0.com) and sign up / log in.
2. Create a new tenant (e.g. `pwamessenger`). Free tier supports up to 7,500 monthly active users.

### 1b. Create a Single Page Application
1. In Auth0 dashboard → Applications → Create Application.
2. Choose **Single Page Application**.
3. Name it (e.g. `PWAMessenger Client`).
4. Note the **Domain** and **Client ID** — these go in User Secrets (see §1e).

### 1c. Enable SMS Passwordless Connection
1. Auth0 dashboard → Authentication → Passwordless → SMS.
2. Enable it. Auth0 handles SMS delivery natively — no external SMS account needed for this use case.

### 1e. Configure Callback URLs
In the Auth0 application settings, set:
- **Allowed Callback URLs:** `https://localhost:7056, https://pwamessenger.pages.dev`
- **Allowed Logout URLs:** `https://localhost:7056, https://pwamessenger.pages.dev`
- **Allowed Web Origins:** `https://localhost:7056, https://pwamessenger.pages.dev`

### 1f. Add Auth0 Credentials to User Secrets
In `PWAMessenger.Api`, add to user secrets:
```json
{
  "Auth0:Domain": "your-tenant.auth0.com",
  "Auth0:ClientId": "your-client-id",
  "Auth0:Audience": "https://your-tenant.auth0.com/api/v2/"
}
```

In `PWAMessenger.Client`, add to user secrets (or `appsettings.Development.json`):
```json
{
  "Auth0:Domain": "your-tenant.auth0.com",
  "Auth0:ClientId": "your-client-id"
}
```

---

## 2. Database Setup

### 2a. Create the Database
Using SSMS or sqlcmd:
```sql
CREATE DATABASE PWAMessenger;
```

### 2b. Run EF Core Migrations
Once the code is scaffolded (already done), create and apply the initial migration:
```bash
# From the repo root
dotnet ef migrations add InitialSchema --project PWAMessenger.Api
dotnet ef database update --project PWAMessenger.Api
```

This creates the `Users`, `InvitedUsers`, and `FcmTokens` tables.

### 2c. Seed Your Phone Number into InvitedUsers
After migrations run, seed your phone number so you can log in:
```sql
INSERT INTO InvitedUsers (PhoneNumber, InvitedAt)
VALUES ('+1XXXXXXXXXX', GETUTCDATE());
```
Use the E.164 format that matches what Auth0 will return in the `sub` claim (`sms|+1XXXXXXXXXX` → phone number is `+1XXXXXXXXXX`).

---

## 3. Polecat (NuGet Package)

Polecat is brand new (announced March 2026) and actively evolving. Verify the current NuGet package before restoring:

```bash
dotnet add package Polecat --project PWAMessenger.Api
```

Check the [Polecat GitHub](https://github.com/JasperFx/polecat) for the latest version and any breaking changes in the API. The `AddPolecat()` registration in `Program.cs` may need to be adjusted as the API stabilizes.

Note: Polecat requires **SQL Server 2025** for its native JSON type support. If your SQL Server is an older version, check the Polecat docs for compatibility notes.

---

## 4. Event Modeling (Before Slice 1 Implementation)

Event modeling is a design step done before writing code. It maps out what events, commands, and read models exist for each feature — what triggers what, and what each user sees.

### Options
- **Online tool:** [app.eventmodeling.org](https://app.eventmodeling.org) — free, purpose-built
- **Miro:** Search for "event modeling" templates in the Miro template library
- **Collaborative:** Describe the use cases in conversation and work through the model together here

### What to model for the walking skeleton
- **Slice 1 (Login and Exist):** Phone entry → InvitedUsers gate → Auth0 OTP → JWT issued → first login display name prompt → `UserRegistered` event → `Users` read model populated → FCM token registered
- **Slice 2 (Send a Message):** User selects recipient → `MessageSent` event appended → async projection fires FCM send → recipient receives OS notification

### Sharing with the agent
A screenshot, exported JSON, or a plain-text description of the event flow works equally well. The key artifact is knowing: for each step, what is the command, what event gets appended, and what read model is updated.

---

## 5. Restore and Build

Once Polecat is added (§3):
```bash
dotnet restore
dotnet build
```

---

## 6. Deploy Checklist Reminder

When the API tunnel URL changes (Dev Tunnels rotates):
- Update the `API_BASE_URL` GitHub Actions secret with the new HTTPS tunnel URL.
