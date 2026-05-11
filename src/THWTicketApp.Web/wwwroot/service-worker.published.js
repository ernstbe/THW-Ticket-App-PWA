// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts("./service-worker-assets.js");
self.addEventListener("install", (event) => event.waitUntil(onInstall(event)));
self.addEventListener("activate", (event) =>
  event.waitUntil(onActivate(event)),
);
self.addEventListener("fetch", (event) => event.respondWith(onFetch(event)));

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
