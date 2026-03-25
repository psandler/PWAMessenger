# Firebase API Key Security

## The key is intentionally public

Firebase web API keys are designed to be embedded in client-side code. Firebase
security is enforced through Security Rules and App Check — not by keeping the
API key secret. Google's own documentation says this explicitly. The automated
scanner email from Google is expected for any public repo containing a Firebase
web config.

## The right fix: add HTTP referrer restrictions

Do NOT regenerate the key. Regenerating breaks the app immediately and requires
updating push.js, both service workers, and removing the old key from git history.

Instead, restrict the key so it only works from your domains.

### Steps in Google Cloud Console

1. Go to **APIs & Services → Credentials**
2. Click the pencil icon on the API key
3. Under **Application restrictions**, select **HTTP referrers (websites)**
4. Add the following referrers:
   ```
   https://psandler.github.io/*
   https://localhost:7056/*
   https://localhost:5271/*
   ```
   Also add your Dev Tunnel URL once known (e.g. `https://xyz-7056.usw2.devtunnels.ms/*`)
5. Under **API restrictions**, select **Restrict key** and enable only:
   - Firebase Installations API
   - Firebase Cloud Messaging Registration API
6. Save

Once restrictions are in place, the key being visible in source code is harmless —
it cannot be used from any origin not on the allowlist.
