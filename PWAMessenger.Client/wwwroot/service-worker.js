// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Push handlers — same as service-worker.published.js so push works via Dev Tunnels in dev.

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
