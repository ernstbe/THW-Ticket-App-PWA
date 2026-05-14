// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts("./service-worker-assets.js");
self.addEventListener("install", (event) => event.waitUntil(onInstall(event)));
self.addEventListener("activate", (event) =>
  event.waitUntil(onActivate(event)),
);
self.addEventListener("fetch", (event) => event.respondWith(onFetch(event)));

// Handle inbound Web Push from the trudesk backend. The server sends a
// JSON payload via web-push (VAPID-signed), parsed here and surfaced as
// a system notification. URL goes into `data.url` so the click handler
// below opens or focuses the right page.
self.addEventListener("push", (event) => {
  // Robust parse: event.data may be missing, may be a non-JSON blob (e.g.
  // a malformed test push), or may be valid JSON but with non-object shape.
  // Whatever happens, we must NOT throw out of the push handler — Chrome
  // will then surface its own "This site has been updated in the background"
  // generic notification, which is worse than our fallback.
  let payload = null;
  if (event.data) {
    try { payload = event.data.json(); }
    catch {
      try { payload = { body: event.data.text() }; } catch { /* binary or unreadable */ }
    }
  }
  if (!payload || typeof payload !== "object") payload = {};

  const title = (typeof payload.title === "string" && payload.title.trim()) || "THW Ticket App";
  const options = {
    body: typeof payload.body === "string" ? payload.body : "",
    tag: typeof payload.tag === "string" ? payload.tag : undefined,
    icon: typeof payload.icon === "string" ? payload.icon : "/app/icon-512.png",
    badge: typeof payload.badge === "string" ? payload.badge : "/app/icon-512.png",
    data: { url: typeof payload.url === "string" ? payload.url : "/app/" }
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

// Handle notification clicks — navigate to the ticket URL
self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = event.notification.data?.url;
  if (!url) return;
  event.waitUntil(
    clients.matchAll({ type: "window", includeUncontrolled: true }).then((windowClients) => {
      for (const client of windowClients) {
        if (client.url.includes(url) && "focus" in client) return client.focus();
      }
      return clients.openWindow(url);
    })
  );
});

const cacheNamePrefix = "offline-cache-";
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
  /\.dll$/,
  /\.pdb$/,
  /\.wasm/,
  /\.html/,
  /\.js$/,
  /\.json$/,
  /\.css$/,
  /\.woff$/,
  /\.png$/,
  /\.jpe?g$/,
  /\.gif$/,
  /\.ico$/,
  /\.blat$/,
  /\.dat$/,
];
const offlineAssetsExclude = [
  /^service-worker\.js$/,
  /^index\.html$/,
  /^404\.html$/,
];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(
  (asset) => new URL(asset.url, baseUrl).href,
);

async function onInstall(event) {
  console.info("Service worker: Install");

  // Fetch and cache all matching items from the assets manifest
  const assetsRequests = self.assetsManifest.assets
    .filter((asset) =>
      offlineAssetsInclude.some((pattern) => pattern.test(asset.url)),
    )
    .filter(
      (asset) =>
        !offlineAssetsExclude.some((pattern) => pattern.test(asset.url)),
    )
    .map(
      (asset) =>
        new Request(asset.url, { integrity: asset.hash, cache: "no-cache" }),
    );
  await caches.open(cacheName).then((cache) => cache.addAll(assetsRequests));
  // Stay in "waiting" until the page tells us to activate (via SKIP_WAITING).
  // index.html shows an update banner; user clicks "Neu laden" to apply.
}

self.addEventListener("message", (event) => {
  if (event.data === "SKIP_WAITING") self.skipWaiting();
});

async function onActivate(event) {
  console.info("Service worker: Activate");

  // Delete unused caches
  const cacheKeys = await caches.keys();
  await Promise.all(
    cacheKeys
      .filter((key) => key.startsWith(cacheNamePrefix) && key !== cacheName)
      .map((key) => caches.delete(key)),
  );

  // Take control of already-open clients without requiring a hard refresh.
  await self.clients.claim();
}

async function onFetch(event) {
  let cachedResponse = null;
  if (event.request.method === "GET") {
    // For all navigation requests, try to serve index.html from cache,
    // unless that request is for an offline resource.
    // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
    const shouldServeIndexHtml =
      event.request.mode === "navigate" &&
      !manifestUrlList.some((url) => url === event.request.url);

    const request = shouldServeIndexHtml ? "index.html" : event.request;
    const cache = await caches.open(cacheName);
    cachedResponse = await cache.match(request);
  }

  return cachedResponse || fetch(event.request);
}
