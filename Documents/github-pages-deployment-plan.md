# Deployment Plan: Blazor WASM Client via GitHub Pages

## Does it work?

Yes. Blazor WebAssembly compiles to static files (HTML, JS, CSS, `.wasm`). GitHub Pages
hosts static files. GitHub Actions builds and deploys automatically on push.

The API stays local and is exposed over HTTPS via Dev Tunnels — the client just needs
to know the tunnel URL.

---

## Two problems to solve first

### 1. Base path

GitHub Pages serves project repos from a sub-path:
`https://psandler.github.io/PWAMessenger/`

Blazor's `index.html` defaults to `<base href="/" />`, which causes all asset
references (`.wasm`, `.dll`, JS) to resolve from the wrong root and break the app.
The deploy workflow must patch this to `/PWAMessenger/` before uploading.

### 2. API URL

The client's `wwwroot/appsettings.json` has `ApiBaseUrl` hardcoded to
`https://localhost:7102`. The deployed client needs the Dev Tunnel URL instead.
Store the tunnel URL as a GitHub Actions secret so it gets injected at build time
without being committed to source.

---

## One-time setup steps

1. **Make the repo public** on GitHub (Settings → General → Danger Zone → Change visibility).

2. **Enable GitHub Pages** — repo Settings → Pages → Source: **GitHub Actions**.
   Leave branch blank; the workflow controls deployment.

3. **Add a GitHub Actions secret** for the API URL:
   - Repo Settings → Secrets and variables → Actions → New repository secret
   - Name: `API_BASE_URL`
   - Value: your Dev Tunnel URL, e.g. `https://xyz-7102.usw2.devtunnels.ms`
   - Update this value whenever the tunnel URL changes (it won't change if you set
     persistence to Persistent in Visual Studio).

---

## Create the workflow file

Create `.github/workflows/deploy-client.yml` with this content:

```yaml
name: Deploy Client to GitHub Pages

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: true

jobs:
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish Blazor client
        run: dotnet publish PWAMessenger.Client/PWAMessenger.Client.csproj -c Release -o publish

      - name: Patch base href for GitHub Pages sub-path
        run: |
          sed -i 's|<base href="/" />|<base href="/PWAMessenger/" />|g' \
            publish/wwwroot/index.html

      - name: Inject API base URL
        run: |
          echo '{ "ApiBaseUrl": "${{ secrets.API_BASE_URL }}" }' \
            > publish/wwwroot/appsettings.json

      - name: Add .nojekyll (prevents Jekyll from ignoring _framework folder)
        run: touch publish/wwwroot/.nojekyll

      - name: Upload Pages artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: publish/wwwroot

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

---

## Per-deploy checklist

- [ ] Dev Tunnel is running and set to Public + Persistent
- [ ] `API_BASE_URL` secret matches the current tunnel URL
- [ ] CORS on the API allows the Pages origin (`https://psandler.github.io`)

For the CORS point: the API's `Program.cs` currently uses `AllowAnyOrigin()`, so
this is already handled.

---

## What the deployed URL will be

`https://psandler.github.io/PWAMessenger/`

Both test devices open this URL. The API runs locally with Dev Tunnels — Device B
does not need to reach `localhost`, only the tunnel URL.

---

## Limitations of this setup

- The API must be running locally with Dev Tunnels active whenever someone tests.
- If the tunnel URL changes, update the `API_BASE_URL` secret and re-run the workflow.
- Push notifications still require the PWA to be installed (added to home screen) on iOS.
