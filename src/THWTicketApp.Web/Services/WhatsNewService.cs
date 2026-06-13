namespace THWTicketApp.Web.Services;

/// <summary>
/// One curated changelog block, usually one per release/deploy-batch.
/// <see cref="Id"/> must increase monotonically — it is what decides
/// whether a device has already seen the entry.
/// </summary>
public sealed record WhatsNewEntry(int Id, string Date, string Title, string[] Items);

/// <summary>
/// Shows a "Was ist neu?" dialog once per device after an app update
/// that ships user-facing changes.
///
/// Deliberately NOT keyed to version.json (the git SHA changes on every
/// deploy, including chore-only ones): instead a curated, hand-written
/// entry list lives here. The dialog only appears when entries exist
/// that the device hasn't seen yet — deploys without a new entry stay
/// silent. Fresh installs are seeded silently (nothing is "new" to a
/// first-time user; they get the onboarding tour instead).
///
/// When shipping user-facing features or notable fixes, append a new
/// <see cref="WhatsNewEntry"/> with the next Id to <see cref="Entries"/>.
/// </summary>
public sealed class WhatsNewService
{
    public const string StorageKey = "whatsnew_last_seen";

    /// <summary>Curated changelog, ascending by Id. Content is German on purpose — primary UI language.</summary>
    public static readonly IReadOnlyList<WhatsNewEntry> Entries =
    [
        new WhatsNewEntry(1, "11.06.2026", "Update vom 11. Juni 2026",
        [
            "Mehrere Zuständige pro Ticket: Über „Weitere Zuständige“ in der Ticket-Ansicht können zusätzliche Personen eingetragen werden. Diese sehen das Ticket auch unter „Meine Tickets“.",
            "Checklisten in Ticket-Vorlagen: Vorlagen können jetzt eine Checkliste enthalten — beim Anwenden landen die Punkte automatisch auf dem neuen Ticket.",
            "Anhänge per Drag & Drop: Dateien können beim Erstellen eines Tickets direkt ins Formular gezogen werden.",
            "Fälligkeitsdatum repariert: Nachträgliches Setzen, Ändern und Löschen funktioniert jetzt zuverlässig — die „Überfällig“-Kachel im Dashboard zeigt damit echte Werte."
        ]),
        new WhatsNewEntry(2, "12.06.2026", "Wiederkehrende Aufgaben",
        [
            "Vorlagen-Auswahl für wiederkehrende Aufgaben: Beim Anlegen oder Bearbeiten kann jetzt eine Ticket-Vorlage gewählt werden — Betreff, Beschreibung, Typ, Priorität und Checkliste werden automatisch übernommen und bleiben einzeln anpassbar."
        ]),
        new WhatsNewEntry(3, "12.06.2026", "Wiederkehrende Aufgaben: Korrekturen",
        [
            "Gruppen-Auswahl für wiederkehrende Aufgaben: Im Dialog kann jetzt die Gruppe des erzeugten Tickets gewählt werden — das Anlegen neuer Aufgaben funktioniert damit zuverlässig."
        ]),
        new WhatsNewEntry(4, "12.06.2026", "Checklisten aus Vorlagen",
        [
            "Checklisten aus Vorlagen werden jetzt direkt beim Erstellen des Tickets angelegt — auch bei Offline-Ticketerstellung gehen sie nicht mehr verloren."
        ]),
        new WhatsNewEntry(5, "12.06.2026", "Prioritäten-Auswahl verbessert",
        [
            "Die Prioritäten-Auswahl passt sich dem gewählten Tickettyp an: Beim Erstellen von Tickets und wiederkehrenden Aufgaben stehen nur noch Prioritäten zur Wahl, die der Typ auch vorsieht.",
            "Prioritäts-Bezeichnungen werden jetzt überall einheitlich übersetzt — auch eigene Prioritäten wie „Very High“ erscheinen nun auf Deutsch."
        ]),
        new WhatsNewEntry(6, "12.06.2026", "Tickets offline erstellen",
        [
            "Neue Tickets können jetzt auch ohne Internetverbindung angelegt werden — inklusive Fälligkeitsdatum und Vorlagen-Checkliste. Sie werden automatisch übertragen, sobald die Verbindung wieder steht."
        ]),
        new WhatsNewEntry(7, "12.06.2026", "Datenexport (DSGVO)",
        [
            "Datenexport: Du kannst deine gespeicherten Daten jetzt als Datei herunterladen (DSGVO-Auskunft) — im Profil unter „Meine Daten“."
        ])
    ];

    public static int LatestId => Entries.Count == 0 ? 0 : Entries.Max(e => e.Id);

    private readonly LocalStorageService _localStorage;
    private readonly OnboardingService _onboarding;

    public bool IsVisible { get; private set; }
    public IReadOnlyList<WhatsNewEntry> VisibleEntries { get; private set; } = [];
    public event Action? StateChanged;

    public WhatsNewService(LocalStorageService localStorage, OnboardingService onboarding)
    {
        _localStorage = localStorage;
        _onboarding = onboarding;
    }

    /// <summary>
    /// Called from MainLayout after auth. Shows unseen entries exactly once
    /// per device. Devices that never stored a marker are either fresh
    /// installs (onboarding not completed → seed silently, the tour runs
    /// instead) or existing installs from before this feature existed
    /// (onboarding completed → show everything once).
    /// </summary>
    public async Task ShowIfUpdatedAsync()
    {
        if (Entries.Count == 0) return;

        var raw = await _localStorage.GetItemAsync(StorageKey);
        int lastSeen;
        if (raw == null)
        {
            var existingInstall = await _onboarding.HasCompletedAsync();
            if (!existingInstall)
            {
                await _localStorage.SetItemAsync(StorageKey, LatestId.ToString());
                return;
            }
            lastSeen = 0;
        }
        else if (!int.TryParse(raw, out lastSeen))
        {
            lastSeen = 0;
        }

        // Never stack on top of the onboarding tour — seed so the user
        // isn't greeted with two overlays; the features are discoverable
        // through the tour and the Settings "Was ist neu?" button.
        if (_onboarding.IsVisible)
        {
            await _localStorage.SetItemAsync(StorageKey, LatestId.ToString());
            return;
        }

        var unseen = Entries.Where(e => e.Id > lastSeen).OrderByDescending(e => e.Id).ToList();
        if (unseen.Count == 0) return;

        VisibleEntries = unseen;
        IsVisible = true;
        StateChanged?.Invoke();
    }

    /// <summary>Open with the full history regardless of stored state (Settings button).</summary>
    public void ShowAll()
    {
        VisibleEntries = Entries.OrderByDescending(e => e.Id).ToList();
        IsVisible = true;
        StateChanged?.Invoke();
    }

    public async Task DismissAsync()
    {
        IsVisible = false;
        await _localStorage.SetItemAsync(StorageKey, LatestId.ToString());
        StateChanged?.Invoke();
    }
}
