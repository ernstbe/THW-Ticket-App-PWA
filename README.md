# THW Ticket App (PWA)

Blazor WebAssembly Progressive Web App zur Ticketverwaltung, entwickelt als Frontend für [Trudesk](https://github.com/polonel/trudesk).

## Features

- **Ticket-Management** - Erstellen, Bearbeiten, Zuweisen, Kommentare, Notizen, Anhänge
- **Kanban Board** - Drag & Drop zwischen Status-Spalten
- **Team Dashboard** - Workload-Übersicht und Statistiken
- **Berichte** - Wochen-Trends, Auflösungszeiten, SLA, Prioritätsverteilung
- **QR/Barcode Scanner** - Kamerabasierte Ticket-Suche
- **Zeiterfassung** - Timer pro Ticket mit Start/Stopp
- **Favoriten & Verknüpfungen** - Tickets markieren und verlinken
- **Gespeicherte Filter** - Filterkonfigurationen speichern und laden
- **Schnellantwort-Vorlagen** - Vordefinierte Templates für Kommentare
- **@Mentions** - Benutzer in Kommentaren erwähnen
- **Offline-Modus** - IndexedDB-Cache mit Sync-Queue und Konflikterkennung
- **Real-time Updates** - Socket.IO Live-Events bei Ticket-Änderungen
- **Browser-Benachrichtigungen** - Web Notifications API
- **Passkey-Auth** - WebAuthn Fingerabdruck/Face/PIN als Login-Alternative
- **Lokalisierung** - Deutsch und Englisch
- **Dark Mode** - Umschaltbar mit Persistierung

## Tech Stack

- [.NET 9.0](https://dotnet.microsoft.com/) / Blazor WebAssembly
- [MudBlazor 9.1](https://mudblazor.com/) (Material Design UI)
- [Markdig](https://github.com/xoofx/markdig) (Markdown-Rendering)
- [Socket.IO Client](https://socket.io/) (Real-time via JS Interop)
- IndexedDB + localStorage (Offline-Speicher)
- WebAuthn / Passkeys (Biometrische Authentifizierung)

## Voraussetzungen

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Trudesk Backend (API v1/v2)

## Quickstart

```bash
git clone https://github.com/ernstbe/THW-Ticket-App-PWA.git
cd THW-Ticket-App-PWA
dotnet build
dotnet run --project src/THWTicketApp.Web
```

Die App startet unter `https://localhost:7173`. Die API-URL kann in den Einstellungen angepasst werden (Standard: `http://localhost:8118/api/v2`).

## Projektstruktur

```
src/
├── THWTicketApp.Shared/       # Shared Models, Interfaces, Helpers
│   ├── Data/                  # PendingAction, etc.
│   ├── Helpers/               # JsonHelper, ErrorHelper, TranslationHelper
│   ├── Models/                # Ticket, User, Status, Priority, etc.
│   └── Services/              # ITrueDeskApiService, ISyncService
│
└── THWTicketApp.Web/          # Blazor WASM PWA
    ├── Components/            # StatusBadge, PriorityBadge, TicketCard
    ├── Layout/                # MainLayout, LoginLayout
    ├── Pages/                 # Alle Seiten (Dashboard, Tickets, Kanban, ...)
    ├── Services/              # API, Sync, Realtime, LocalStorage, Localization, ...
    └── wwwroot/
        └── js/                # JS Interop (IndexedDB, Socket.IO, Scanner, WebAuthn, Notifications)
```

## API-Kompatibilität

Die App unterstützt sowohl Trudesk API v1 als auch v2:
- **v2**: JWT Bearer Auth, `{ ticket: {...} }` Wrapper, `/api/v2/accounts`
- **v1**: accesstoken Header, flache Payloads, `/api/v1/users`
- Endpoints die nur in v1 existieren (Comments, Notes, Stats, Notifications) werden automatisch über v1 geroutet

## Lizenz

[MIT](LICENSE)
