import { initializeApp } from 'firebase/app';
import { getMessaging, getToken } from 'firebase/messaging';

const firebaseConfig = {
    apiKey: "AIzaSyC6L2f52jw9jEgr7_sp8HTFTPy35UbNZ6k",
    authDomain: "pwamessenger.firebaseapp.com",
    projectId: "pwamessenger",
    storageBucket: "pwamessenger.firebasestorage.app",
    messagingSenderId: "545813805442",
    appId: "1:545813805442:web:4433f1e29ce4307165b79b"
};

const app = initializeApp(firebaseConfig);
const messaging = getMessaging(app);

export async function requestPermissionAndGetToken(vapidKey) {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') return null;

    // Pass Blazor's already-registered service worker so Firebase doesn't
    // look for a separate firebase-messaging-sw.js file.
    const registration = await navigator.serviceWorker.ready;
    return await getToken(messaging, { vapidKey, serviceWorkerRegistration: registration });
}
