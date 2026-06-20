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
        ]),
        new WhatsNewEntry(8, "12.06.2026", "Neue Statistik-Seite",
        [
            "Neue Statistik-Seite: Ticketaufkommen, Status-Verteilung und Auslastung auf einen Blick — erreichbar über den Menüpunkt „Statistik“."
        ]),
        new WhatsNewEntry(9, "13.06.2026", "Leerzeilen in Beschreibungen",
        [
            "Leerzeilen in der Ticket-Beschreibung bleiben jetzt erhalten — beim Speichern und beim erneuten Bearbeiten wird der Text so dargestellt, wie du ihn eingegeben hast."
        ]),
        new WhatsNewEntry(10, "13.06.2026", "Verknüpfte Tickets",
        [
            "Tickets können jetzt miteinander verknüpft werden (verwandt, blockiert, Duplikat) — in der Ticket-Ansicht unter „Verknüpfte Tickets“. Die Verknüpfung erscheint automatisch auch beim verknüpften Ticket."
        ]),
        new WhatsNewEntry(11, "13.06.2026", "Profilbild",
        [
            "Du kannst jetzt ein eigenes Profilbild hochladen — im Profil unter „Profilbild ändern“."
        ]),
        new WhatsNewEntry(12, "13.06.2026", "Mehrere Tickets auf einmal bearbeiten",
        [
            "In der Ticketliste kannst du über die Mehrfachauswahl jetzt mehrere Tickets gleichzeitig einer Person zuweisen. Vor dem Sammel-Löschen kommt außerdem eine Sicherheitsabfrage."
        ]),
        new WhatsNewEntry(13, "16.06.2026", "Statistik respektiert Gruppen",
        [
            "Die Statistik-Seite zeigt jetzt nur noch Zahlen aus den Gruppen, die du auch in der Ticketliste sehen darfst. Kennzahlen, Verlauf und Auslastung pro Person sind damit auf deinen Bereich beschränkt — die Jugend sieht keine Stab-Zahlen mehr und umgekehrt."
        ]),
        new WhatsNewEntry(14, "20.06.2026", "Leerzeilen in Kommentaren",
        [
            "Leerzeilen in Kommentaren und Notizen bleiben jetzt erhalten und werden korrekt angezeigt — vorher wurden sie beim Speichern zusammengefasst. Gilt für neue Kommentare; bereits gespeicherte lassen sich nicht nachträglich wiederherstellen."
        ]),
        new WhatsNewEntry(15, "20.06.2026", "Bearbeiten wird nicht mehr unterbrochen",
        [
            "Wenn jemand anderes ein Ticket ändert, während du gerade Titel oder Beschreibung bearbeitest, wird deine Eingabe nicht mehr überschrieben. Die Aktualisierung wird nachgeholt, sobald du fertig bist — und im Hintergrund lädt die Ansicht ohne störendes Aufblitzen nach."
        ]),
        new WhatsNewEntry(16, "20.06.2026", "Profilbilder in der Aktivität",
        [
            "Im Aktivitätsverlauf eines Tickets erscheint jetzt das Profilbild der jeweiligen Person neben Kommentaren, Notizen und Verlaufseinträgen. Wer kein Bild hochgeladen hat, bekommt weiterhin die farbigen Initialen."
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
