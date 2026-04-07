// Minimal service worker for PWA install support.
// Blazor Server requires a live connection, so we don't cache offline resources.
self.addEventListener('install', event => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', event => { });
