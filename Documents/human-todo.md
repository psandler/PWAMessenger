# Human Setup Checklist

Tasks that require human action before or alongside implementation. Work through these roughly in order.

---

## 1. Auth0 Setup

Auth0 provides email passwordless authentication. Auth0 handles email delivery natively — no external email provider account needed.

### 1a. Create Auth0 Tenant
1. Go to [auth0.com](https://auth0.com) and sign up / log in.
2. Create a new tenant (e.g. `pwamessenger`). Free tier supports up to 7,500 monthly active users.

### 1b. Create a Single Page Application
1. In Auth0 dashboard → Applications → Create Application.
2. Choose **Single Page Application**.
3. Name it (e.g. `PWAMessenger Client`).
4. Note the **Domain** and **Client ID** — these go in User Secrets (see §1d).

### 1c. Enable Email Passwordless Connection
1. Auth0 dashboard → Authentication → Passwordless → Email.
2. Enable it.
3. Choose **Magic Link** or **One-Time Code** — either works. Magic Link is smoother UX (one click in email); One-Time Code matches the original SMS OTP experience.
4. No external provider setup required — Auth0 delivers the email.

### 1d. Configure Callback URLs
In the Auth0 application settings, set:
- **Allowed Callback URLs:** `https://localhost:7056, https://pwamessenger.pages.dev`
- **Allowed Logout URLs:** `https://localhost:7056, https://pwamessenger.pages.dev`
- **Allowed Web Origins:** `https://localhost:7056, https://pwamessenger.pages.dev`

### 1e. Add Auth0 Credentials to User Secrets
In `PWAMessenger.Api`, add to user secrets:
```json
{
  "Auth0:Domain": "your-tenant.auth0.com",
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

### 2c. Seed Your Email into InvitedUsers
After migrations run, seed your email address so you can log in:
```sql
INSERT INTO InvitedUsers (Email, InvitedAt)
VALUES ('you@example.com', GETUTCDATE());
```
Use the exact email address you will authenticate with via Auth0.

---

## 3. Polecat (NuGet Package)

Polecat is brand new (announced March 2026) and actively evolving. Verify the current NuGet package before restoring:

```bash
dotnet add package Polecat --project PWAMessenger.Api
```

Check the [Polecat GitHub](https://github.com/JasperFx/polecat) for the latest version and any breaking changes in the API. The `AddPolecat()` registration in `Program.cs` may need to be adjusted as the API stabilizes.

Note: Polecat requires **SQL Server 2025** for its native JSON type support. If your SQL Server is an older version, check the Polecat docs for compatibility notes.

---

## 4. Restore and Build

Once Polecat is added (§3):
```bash
dotnet restore
dotnet build
```

---

## 5. Deploy Checklist Reminder

When the API tunnel URL changes (Dev Tunnels rotates):
- Update the `API_BASE_URL` GitHub Actions secret with the new HTTPS tunnel URL.
