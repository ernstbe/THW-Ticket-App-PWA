# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Blazor WebAssembly PWA frontend for the [Trudesk](https://github.com/polonel/trudesk) ticketing backend. .NET 9.0, MudBlazor 9.1. Successor to the (deprecated) MAUI client `THW-Ticket-App/`. Primary UI language is German.

## Common commands

Run from the repo root:

```bash
dotnet restore
dotnet build                                                    # solution-wide
dotnet run --project src/THWTicketApp.Web                       # dev server, https://localhost:7173
dotnet test                                                     # all tests
dotnet test --filter "FullyQualifiedName~SyncServiceTests"      # one class
dotnet test --filter "FullyQualifiedName~SyncServiceTests.SyncesAddCommentAction"  # one test
dotnet format                                                   # CI verifies with --verify-no-changes
dotnet publish src/THWTicketApp.Web -c Release -o publish       # WASM static output → publish/wwwroot
```

Docker: `docker build -t thw-ticket-app-pwa .` (multi-stage: SDK build → nginx:alpine serving `wwwroot`, port 80, see `nginx.conf`).

## Architecture

### Solution layout

- `src/THWTicketApp.Shared/` — models, interfaces, helpers consumed by both Web and Tests. No Blazor/JSInterop dependencies; this is the boundary that keeps test surface portable.
- `src/THWTicketApp.Web/` — the Blazor WASM app. `Program.cs` wires DI; pages live under `Pages/`, services under `Services/`, JS interop modules under `wwwroot/js/`.
- `tests/THWTicketApp.Tests/` — xUnit + bUnit + NSubstitute. `THWTicketApp.Web` exposes `InternalsVisibleTo("THWTicketApp.Tests")` so tests can reach internals.

### Trudesk API: dual v1/v2 client

`TrueDeskApiService` (implements `ITrueDeskApiService`) is the single API client and the most important file to understand before changing anything network-related.

- `AppSettings.ApiBaseUrl` is user-configurable at runtime (Settings page) and persisted via `LocalStorageService` under key `settings_apiurl`. `AppSettingsInitializer` loads it on first render; default is `{origin}/api/v1`.
- The `IsV2` flag is derived from whether `ApiBaseUrl` contains `/api/v2`. Auth header switches accordingly: v2 uses `Authorization: Bearer <jwt>`, v1 uses `accesstoken: <token>`.
- v2 login response is `{ token, refreshToken }`; user id is parsed from the JWT payload when not present in the response body. v1 returns `{ accessToken }`.
- Some endpoints exist only in v1 (Comments, Notes, Stats, Notifications) — these are routed via `V1BaseUrl` (derived by stripping `/api/v2` from the configured base). Newer CRUD endpoints (Teams, Departments, Templates, Calendar, Recurring Tasks, Assets) are v2-only and routed via `V2BaseUrl`. **Do not assume the configured base URL is the right one for a given call** — pick `BaseUrl`, `V1BaseUrl`, or `V2BaseUrl` based on which API version actually exposes the endpoint.
- v2 ticket payloads are wrapped (`{ ticket: {...} }`); v1 is flat. v2 user endpoints are `/accounts`, v1 is `/users`.

Auth integrates with Blazor's `AuthenticationStateProvider` via `AuthStateProvider`, which calls `TryRestoreSessionAsync()` on first state query. Routing uses `AuthorizeRouteView` + `RedirectToLogin`.

### Offline-first sync

`SyncService` + `IndexedDbService` implement the offline pipeline:

1. UI mutations enqueue a `PendingActionDto` (typed actions: `AddComment`, `AddNote`, ticket edits, etc.) into IndexedDB via the `js/indexeddb-interop.js` module.
2. When online, `SyncService.ProcessQueueAsync` drains the queue, applying actions through `ITrueDeskApiService`. Successes are removed; failures are retried using a fixed backoff schedule (`RetryDelays`: 2s, 8s, 30s, 2m, 10m, 30m). After 6 failed attempts, the action is dropped.
3. Before applying a mutation that targets an existing ticket, `CheckConflictAsync` compares the locally captured `TicketUpdatedAt` against the server's current value. Mismatches mark the action `IsConflicted` and raise `ConflictDetected`; the user resolves these on the `SyncConflicts` page (force-apply or discard).
4. Attachments larger than `SyncService.MaxAttachmentBytes` (5 MB) are rejected at enqueue time.

`AppStateService` is the cross-component event bus for connection state, pending count, and unread notifications.

### Realtime (Socket.IO)

`RealtimeService` wraps `wwwroot/js/realtime-interop.js` (Socket.IO client) via JSInterop. It exposes per-channel C# events (`TicketUpdated`, `StatusUpdated`, `CommentNoteChanged`, `NotificationUpdate`, etc.) plus an `AnyTicketChanged` fan-in for list pages. Connects against `ServerUrl` (the API base with `/api/v1` or `/api/v2` stripped) using the stored auth token.

### JS interop modules (`wwwroot/js/`)

Each module is loaded on demand via `import('./js/<module>.js')` from a paired C# service. Don't add interop calls inline in pages — extend the existing service or create a new one, and keep the JS module focused.

- `indexeddb-interop.js` ↔ `IndexedDbService` (ticket cache + pending action queue)
- `realtime-interop.js` ↔ `RealtimeService` (Socket.IO)
- `storage-interop.js` ↔ `LocalStorageService`
- `notification-interop.js` ↔ `BrowserNotificationService` (Web Notifications API)
- `webauthn-interop.js` (Passkey login)
- `scanner-interop.js` (camera-based QR/Barcode lookup)
- `keyboard-shortcuts.js`

### Localization

`LocalizationService` holds two in-code dictionaries (`De`, `En`) and exposes `T(key)`. Default language is `de`; user choice persists in localStorage under `settings_language`. New user-facing strings should go through `T()`.

## Conventions

- Conventional Commits (`feat(...)`, `fix(...)`, `ui(...)`, `chore(...)`); the recent log shows scopes like `auth`, `api`, `kanban`, `settings`, `login`. Match the existing style when committing.
- When shipping user-facing features or notable fixes, append a `WhatsNewEntry` (German text, next Id) to `WhatsNewService.Entries` — that's what feeds the in-app "Was ist neu?" dialog shown once per device after the deploy. Deploys without a new entry stay silent.
- Default branch is `master`; PRs merge into it. CI (`.github/workflows/build.yml`) runs build, test, `dotnet format --verify-no-changes`, and a vulnerable-package scan on push/PR to master.
- C# files use `Nullable` and `ImplicitUsings` enabled — don't disable per-file.
- Service registration: most services are `Scoped` (Blazor WASM has a single scope per user, so this is effectively singleton); `AppSettings` is the only `Singleton`. Follow this pattern when adding services and remember to register both the concrete type and any interface (see `IndexedDbService` / `IIndexedDbService`).
- Tests for HTTP behavior use `CapturingHttpMessageHandler` (in `tests/.../Helpers/`) rather than mocking `HttpClient` directly — reuse it.
