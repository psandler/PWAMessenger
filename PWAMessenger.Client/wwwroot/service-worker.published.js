// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

// ---- Push notification handlers ----

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = event.notification.data?.url || '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
            const existing = windowClients.find(c => c.url.includes(self.origin));
            if (existing) return existing.focus();
            return clients.openWindow(url);
        })
    );
});

// Firebase compat SDK handles the 'push' event and calls onBackgroundMessage.
// Using importScripts (compat) because ES modules are not reliably supported in service workers.
// Wrapped in try-catch: a CDN failure must not break the Blazor caching/offline logic below.
try {
    importScripts('https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js');
    importScripts('https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging-compat.js');

    firebase.initializeApp({
        apiKey: "AIzaSyC6L2f52jw9jEgr7_sp8HTFTPy35UbNZ6k",
        authDomain: "pwamessenger.firebaseapp.com",
        projectId: "pwamessenger",
        storageBucket: "pwamessenger.firebasestorage.app",
        messagingSenderId: "545813805442",
        appId: "1:545813805442:web:4433f1e29ce4307165b79b"
    });

    const _messaging = firebase.messaging();

    _messaging.onBackgroundMessage(payload => {
        const title = payload.notification?.title ?? 'New message';
        const body = payload.notification?.body ?? '';
        const url = payload.data?.url ?? '/';
        self.registration.showNotification(title, {
            body,
            icon: '/icon-192.png',
            data: { url }
        });
    });
} catch (e) {
    console.warn('Service worker: Firebase initialization failed — push notifications unavailable.', e);
}

// ---- End push notification handlers ----


self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/, /^appsettings.*\.json$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    try {
        let cachedResponse = null;
        if (event.request.method === 'GET') {
            // For all navigation requests, try to serve index.html from cache,
            // unless that request is for an offline resource.
            // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
            const shouldServeIndexHtml = event.request.mode === 'navigate'
                && !manifestUrlList.some(url => url === event.request.url);

            const request = shouldServeIndexHtml ? 'index.html' : event.request;
            const cache = await caches.open(cacheName);
            cachedResponse = await cache.match(request);
        }

        // Use `request` not `event.request`: for navigation requests shouldServeIndexHtml
        // rewrites request to 'index.html'. Falling back to event.request would fetch the
        // original navigation URL with redirect:manual, producing an opaque redirect and ERR_FAILED.
        return cachedResponse || fetch(request);
    } catch {
        // Cache lookup failed — fall back to network so a broken cache never produces ERR_FAILED.
        return fetch(event.request);
    }
}
