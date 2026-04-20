# Human Setup Checklist

Tasks that require human action before or alongside implementation. Work through these roughly in order.

---

## 0. Event Modeling Tooling (emlang)

emlang is used to generate visual event model diagrams from YAML source files before any slice is coded. Review the diagram and sign off before implementation begins.

### One-time setup

Install the emlang CLI:
```powershell
go install github.com/emlang-project/emlang/cmd/emlang@latest
```

Ensure `C:\Users\<you>\go\bin` is on your PATH (add via System Environment Variables if needed).

### Generating diagrams

Run from the repo root. Output goes to `Documents/event-models/emlang-output/` (gitignored):

```powershell
mkdir -Force Documents\event-models\emlang-output

emlang diagram "Documents\event-models\milestone-1.yaml" -o "Documents\event-models\emlang-output\milestone-1.html"
emlang diagram "Documents\event-models\milestone-2.yaml" -o "Documents\event-models\emlang-output\milestone-2.html"
```

Open to review:
```powershell
start Documents\event-models\emlang-output\milestone-1.html
start Documents\event-models\emlang-output\milestone-2.html
```

Add a new command for each new milestone YAML as the project grows.

### Linting

```powershell
emlang lint "Documents\event-models\milestone-1.yaml"
```

Warnings for query-only slices (`slice-missing-event`) and boundary-crossing commands (`command-without-event`) are intentional — they are documented with comments in the YAML files.

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
4. Note the **Domain** and **Client ID** — these go in User Secrets (see §1e).

### 1c. Enable Email Passwordless Connection
1. Auth0 dashboard → Authentication → Passwordless → Email.
2. Enable it.
3. Choose **Magic Link** or **One-Time Code** — either works. Magic Link is smoother UX (one click in email); One-Time Code is quicker to enter.
4. No external provider setup required — Auth0 delivers the email.

### 1d. Configure Callback URLs
In the Auth0 application settings, set:
- **Allowed Callback URLs:** `https://localhost:7056, https://pwamessenger.pages.dev`
- **Allowed Logout URLs:** `https://localhost:7056, https://pwamessenger.pages.dev`
- **Allowed Web Origins:** `https://localhost:7056, https://pwamessenger.pages.dev`

If using Dev Tunnels for local API testing, add the tunnel URL to each list as well.

### 1e. Add Auth0 Credentials to User Secrets
In `PWAMessenger.Api`, add to user secrets:
```json
{
  "Auth0:Domain": "your-tenant.auth0.com",
  "Auth0:Audience": "pwamessenger"
}
```

In `PWAMessenger.Client` (`appsettings.Development.json`):
```json
{
  "Auth0:Domain": "your-tenant.auth0.com",
  "Auth0:ClientId": "your-client-id",
  "Auth0:Audience": "pwamessenger",
  "Firebase:VapidKey": "your-vapid-key"
}
```

### 1f. Create Auth0 API
In Auth0 dashboard → Applications → APIs → Create API:
- **Name:** PWAMessenger
- **Identifier (audience):** `pwamessenger`

### 1g. Add Post Login Action to inject email claim
Auth0 does not include `email` in access tokens by default. Add a Post Login Action:
```javascript
exports.onExecutePostLogin = async (event, api) => {
  if (event.authorization) {
    api.accessToken.setCustomClaim('email', event.user.email);
  }
};
```
Without this, user registration will fail (the API cannot identify the user's email from the JWT).

---

## 2. Database Setup

### 2a. Create the Database
Using SSMS or sqlcmd:
```sql
CREATE DATABASE PWAMessenger;
```

### 2b. Apply EF Core Migrations
Migration files are already in the repo. Just apply them:
```bash
dotnet ef database update --project PWAMessenger.Api
```

This creates the `Users`, `InvitedUsers`, and `FcmTokens` tables. Polecat creates its own tables (`pc_*`) automatically on first startup.

### 2c. Seed Your Email into InvitedUsers
After migrations run, seed your email address so you can log in:
```sql
INSERT INTO InvitedUsers (Email, InvitedAt)
VALUES ('you@example.com', GETUTCDATE());
```
Use the exact email address you will authenticate with via Auth0.

---

## 3. Polecat

Polecat 1.4.0 is already added to the project and the integration patterns are verified. No additional setup required — the `pc_*` tables are created automatically on first API startup via `ApplyAllDatabaseChangesOnStartup()`.

Reference: [Polecat GitHub](https://github.com/JasperFx/polecat)

---

## 4. Restore and Build

```bash
dotnet restore
dotnet build
```

---

## 5. API Tunnel (Dev Tunnels)

The API must be reachable over HTTPS from the deployed Cloudflare Pages client. Use the `devtunnel` CLI for a **persistent URL** that survives restarts.

### One-time setup

```bash
devtunnel user login
devtunnel create pwamessenger-api --allow-anonymous
devtunnel port create pwamessenger-api -p 7102 --protocol https
```

After creation, run `devtunnel show pwamessenger-api` to get the stable URL — it will look like `https://pwamessenger-api-7102.<region>.devtunnels.ms`. Set this as the `API_BASE_URL` GitHub Actions secret. It will not change unless you delete and recreate the tunnel.

Also add the tunnel URL to Auth0's **Allowed Callback URLs**, **Allowed Logout URLs**, and **Allowed Web Origins** (see §1d).

### Every time you want to expose the API

```bash
devtunnel host pwamessenger-api
```

Stop with Ctrl+C. The tunnel URL remains the same on the next start.

> **Do not use** `devtunnel host -p 7102 --allow-anonymous` — that creates a temporary tunnel with a new URL every time, requiring you to update the GitHub secret and Auth0 settings on every restart.
