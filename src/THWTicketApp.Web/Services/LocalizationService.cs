namespace THWTicketApp.Web.Services;

public class LocalizationService
{
    private readonly LocalStorageService _localStorage;
    private string _currentLanguage = "de";
    private bool _initialized;

    public event Action? LanguageChanged;
    public string CurrentLanguage => _currentLanguage;

    public LocalizationService(LocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        var lang = await _localStorage.GetItemAsync("settings_language");
        if (!string.IsNullOrEmpty(lang)) _currentLanguage = lang;
        _initialized = true;
    }

    public async Task SetLanguageAsync(string language)
    {
        _currentLanguage = language;
        await _localStorage.SetItemAsync("settings_language", language);
        LanguageChanged?.Invoke();
    }

    public string T(string key) => Get(key, _currentLanguage);

    private static string Get(string key, string lang)
    {
        if (lang == "en" && En.TryGetValue(key, out var en)) return en;
        if (De.TryGetValue(key, out var de)) return de;
        return key;
    }

    private static readonly Dictionary<string, string> De = new()
    {
        // Navigation
        ["nav.dashboard"] = "Dashboard",
        ["nav.tickets"] = "Tickets",
        ["nav.newticket"] = "Neues Ticket",
        ["nav.kanban"] = "Kanban Board",
        ["nav.team"] = "Team",
        ["nav.reports"] = "Berichte",
        ["nav.scanner"] = "Scanner",
        ["nav.notifications"] = "Benachrichtigungen",
        ["nav.syncconflicts"] = "Sync-Konflikte",
        ["nav.settings"] = "Einstellungen",

        // Common
        ["common.save"] = "Speichern",
        ["common.cancel"] = "Abbrechen",
        ["common.delete"] = "Löschen",
        ["common.edit"] = "Bearbeiten",
        ["common.close"] = "Schließen",
        ["common.open"] = "Öffnen",
        ["common.search"] = "Suchen",
        ["common.filter"] = "Filter",
        ["common.refresh"] = "Aktualisieren",
        ["common.loading"] = "Laden...",
        ["common.error"] = "Fehler",
        ["common.success"] = "Erfolgreich",
        ["common.all"] = "Alle",
        ["common.yes"] = "Ja",
        ["common.no"] = "Nein",
        ["common.back"] = "Zurück",
        ["common.logout"] = "Abmelden",

        // Login
        ["login.title"] = "Anmelden",
        ["login.username"] = "Benutzername",
        ["login.password"] = "Passwort",
        ["login.submit"] = "Anmelden",
        ["login.submitting"] = "Anmelden...",
        ["login.failed"] = "Anmeldung fehlgeschlagen. Bitte Benutzername und Passwort prüfen.",
        ["login.connection_error"] = "Verbindungsfehler",
        ["login.passkey"] = "Mit Passkey anmelden",
        ["login.passkey_failed"] = "Passkey-Anmeldung fehlgeschlagen.",
        ["login.username_required"] = "Benutzername ist erforderlich",
        ["login.password_required"] = "Passwort ist erforderlich",

        // Tickets
        ["tickets.title"] = "Tickets",
        ["tickets.new"] = "Neues Ticket",
        ["tickets.search"] = "Tickets suchen...",
        ["tickets.open"] = "Offen",
        ["tickets.pending"] = "In Bearbeitung",
        ["tickets.closed"] = "Geschlossen",
        ["tickets.mytickets"] = "Meine Tickets",
        ["tickets.sort"] = "Sortierung",
        ["tickets.sort.newest"] = "Erstellt (neueste)",
        ["tickets.sort.oldest"] = "Erstellt (älteste)",
        ["tickets.sort.updated"] = "Zuletzt aktualisiert",
        ["tickets.sort.priority"] = "Priorität",
        ["tickets.sort.subject"] = "Betreff (A-Z)",
        ["tickets.none"] = "Keine Tickets gefunden.",
        ["tickets.assigned"] = "Ticket zugewiesen.",
        ["tickets.closed_success"] = "Ticket geschlossen.",
        ["tickets.savefilter"] = "Filter speichern",
        ["tickets.loadfilter"] = "Gespeicherte Filter",
        ["tickets.deletefilter"] = "Filter löschen",
        ["tickets.filtername"] = "Filtername",

        // Ticket Detail
        ["detail.description"] = "Beschreibung",
        ["detail.no_description"] = "Keine Beschreibung.",
        ["detail.edit"] = "Ticket bearbeiten",
        ["detail.subject"] = "Betreff",
        ["detail.status"] = "Status",
        ["detail.priority"] = "Priorität",
        ["detail.creator"] = "Ersteller",
        ["detail.assignee"] = "Zuständig",
        ["detail.unassigned"] = "Nicht zugewiesen",
        ["detail.group"] = "Gruppe",
        ["detail.type"] = "Typ",
        ["detail.created"] = "Erstellt",
        ["detail.updated"] = "Aktualisiert",
        ["detail.due"] = "Fällig",
        ["detail.assign_to"] = "Zuweisen an",
        ["detail.assign"] = "Zuweisen",
        ["detail.remove_assign"] = "Entfernen",
        ["detail.tags"] = "Tags",
        ["detail.linked"] = "Verknüpfte Tickets",
        ["detail.no_links"] = "Keine Verknüpfungen.",
        ["detail.link_related"] = "Verwandt",
        ["detail.link_blocks"] = "Blockiert",
        ["detail.link_blocked_by"] = "Blockiert von",
        ["detail.link_duplicate"] = "Duplikat",
        ["detail.subscribe"] = "Abonnieren",
        ["detail.unsubscribe"] = "Abbestellen",
        ["detail.subscribed"] = "Ticket abonniert.",
        ["detail.unsubscribed"] = "Abo entfernt.",
        ["detail.delete_ticket"] = "Ticket löschen",
        ["detail.not_found"] = "Ticket nicht gefunden.",

        // Comments & Notes
        ["comments.title"] = "Kommentare",
        ["comments.new"] = "Neuer Kommentar",
        ["comments.send"] = "Kommentar senden",
        ["comments.added"] = "Kommentar hinzugefügt.",
        ["comments.quickreply"] = "Schnellantwort einfügen",
        ["comments.mention"] = "Erwähnen",
        ["notes.title"] = "Notizen",
        ["notes.new"] = "Neue Notiz",
        ["notes.add"] = "Notiz hinzufügen",
        ["notes.added"] = "Notiz hinzugefügt.",

        // Attachments
        ["attachments.title"] = "Anhänge",
        ["attachments.none"] = "Keine Anhänge.",
        ["attachments.upload"] = "Datei hochladen",
        ["attachments.uploading"] = "Wird hochgeladen...",
        ["attachments.uploaded"] = "Datei hochgeladen.",
        ["attachments.deleted"] = "Anhang gelöscht.",
        ["attachments.name"] = "Name",
        ["attachments.type"] = "Typ",
        ["attachments.size"] = "Größe",
        ["attachments.date"] = "Datum",
        ["attachments.actions"] = "Aktionen",

        // Time Tracking
        ["time.title"] = "Zeiterfassung",
        ["time.timer"] = "Timer",
        ["time.start"] = "Starten",
        ["time.stop"] = "Stoppen",
        ["time.description"] = "Beschreibung (optional)",
        ["time.total"] = "Gesamt",
        ["time.no_entries"] = "Keine Zeiteinträge.",
        ["time.col_start"] = "Start",
        ["time.col_duration"] = "Dauer",
        ["time.col_description"] = "Beschreibung",

        // History
        ["history.title"] = "Verlauf",

        // Assets
        ["nav.assets"] = "Ausstattung",
        ["assets.new"] = "Neues Asset",
        ["assets.name"] = "Name",
        ["assets.tag"] = "Asset-Tag",
        ["assets.category"] = "Kategorie",
        ["assets.location"] = "Standort",
        ["assets.none"] = "Keine Assets vorhanden.",
        ["assets.name_required"] = "Name ist erforderlich.",
        ["assets.delete_confirm"] = "Asset wirklich löschen?",

        // Recurring Tasks
        ["nav.recurring"] = "Wiederkehrende Aufgaben",
        ["recurring.new"] = "Neue Aufgabe",
        ["recurring.active"] = "Aktiv",
        ["recurring.inactive"] = "Inaktiv",
        ["recurring.schedule"] = "Zeitplan",
        ["recurring.ticket_subject"] = "Ticket-Betreff",
        ["recurring.ticket_template"] = "Ticket-Vorlage",
        ["recurring.schedule_type"] = "Wiederholungsart",
        ["recurring.monthly"] = "Monatlich",
        ["recurring.quarterly"] = "Quartalsweise",
        ["recurring.annual"] = "Jährlich",
        ["recurring.day_of_month"] = "Tag im Monat",
        ["recurring.days_before"] = "Tage vor Fälligkeit",
        ["recurring.enabled"] = "Aktiviert",
        ["recurring.next_run"] = "Nächste Ausführung",
        ["recurring.none"] = "Keine wiederkehrenden Aufgaben.",
        ["recurring.name_required"] = "Name ist erforderlich.",
        ["recurring.delete_confirm"] = "Aufgabe wirklich löschen?",

        // Reports
        ["reports.handover"] = "Übergabebericht",
        ["reports.sitzung"] = "OV-Sitzungsbericht",

        // Settings
        ["settings.title"] = "Einstellungen",
        ["settings.appearance"] = "Erscheinungsbild",
        ["settings.darkmode"] = "Dark Mode",
        ["settings.notifications"] = "Benachrichtigungen",
        ["settings.notifications_desc"] = "Über neue Tickets und Änderungen informieren",
        ["settings.notify_new"] = "Neue Tickets",
        ["settings.notify_comments"] = "Kommentare",
        ["settings.notify_status"] = "Statusänderungen",
        ["settings.notify_mine"] = "Nur meine Tickets",
        ["settings.notify_mine_desc"] = "Nur bei mir zugewiesenen Tickets",
        ["settings.notify_highprio"] = "Nur hohe Priorität",
        ["settings.notify_highprio_desc"] = "Nur bei hoher/dringender Priorität",
        ["settings.language"] = "Sprache / Language",
        ["settings.api"] = "API-Konfiguration",
        ["settings.api_url"] = "Server-URL",
        ["settings.timeout"] = "Verbindungs-Timeout (Sekunden)",
        ["settings.test_connection"] = "Verbindung testen",
        ["settings.testing"] = "Teste...",
        ["settings.cache"] = "Offline-Cache",
        ["settings.cache_enable"] = "Offline-Caching aktivieren",
        ["settings.cache_desc"] = "Tickets lokal speichern für Offline-Zugriff",
        ["settings.cache_clear"] = "Cache leeren",
        ["settings.saved"] = "Einstellungen gespeichert.",
        ["settings.passkey"] = "Passkey / Biometrie",
        ["settings.passkey_register"] = "Passkey registrieren",
        ["settings.passkey_registered"] = "Passkey registriert",
        ["settings.passkey_remove"] = "Passkey entfernen",
        ["settings.passkey_desc"] = "Schnelle Anmeldung per Fingerabdruck, Gesicht oder Geräte-PIN",
        ["settings.passkey_not_supported"] = "Ihr Browser unterstützt keine Passkeys.",
        ["settings.about"] = "Über",

        // Accessibility
        ["a11y.toggle_menu"] = "Menü umschalten",
        ["a11y.notifications"] = "Benachrichtigungen",
        ["a11y.toggle_darkmode"] = "Dark Mode umschalten",
        ["a11y.logout"] = "Abmelden",
        ["a11y.main_navigation"] = "Hauptnavigation",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        // Navigation
        ["nav.dashboard"] = "Dashboard",
        ["nav.tickets"] = "Tickets",
        ["nav.newticket"] = "New Ticket",
        ["nav.kanban"] = "Kanban Board",
        ["nav.team"] = "Team",
        ["nav.reports"] = "Reports",
        ["nav.scanner"] = "Scanner",
        ["nav.notifications"] = "Notifications",
        ["nav.syncconflicts"] = "Sync Conflicts",
        ["nav.settings"] = "Settings",

        // Common
        ["common.save"] = "Save",
        ["common.cancel"] = "Cancel",
        ["common.delete"] = "Delete",
        ["common.edit"] = "Edit",
        ["common.close"] = "Close",
        ["common.open"] = "Open",
        ["common.search"] = "Search",
        ["common.filter"] = "Filter",
        ["common.refresh"] = "Refresh",
        ["common.loading"] = "Loading...",
        ["common.error"] = "Error",
        ["common.success"] = "Success",
        ["common.all"] = "All",
        ["common.yes"] = "Yes",
        ["common.no"] = "No",
        ["common.back"] = "Back",
        ["common.logout"] = "Logout",

        // Login
        ["login.title"] = "Sign In",
        ["login.username"] = "Username",
        ["login.password"] = "Password",
        ["login.submit"] = "Sign In",
        ["login.submitting"] = "Signing in...",
        ["login.failed"] = "Login failed. Please check your username and password.",
        ["login.connection_error"] = "Connection error",
        ["login.passkey"] = "Sign in with Passkey",
        ["login.passkey_failed"] = "Passkey authentication failed.",
        ["login.username_required"] = "Username is required",
        ["login.password_required"] = "Password is required",

        // Tickets
        ["tickets.title"] = "Tickets",
        ["tickets.new"] = "New Ticket",
        ["tickets.search"] = "Search tickets...",
        ["tickets.open"] = "Open",
        ["tickets.pending"] = "In Progress",
        ["tickets.closed"] = "Closed",
        ["tickets.mytickets"] = "My Tickets",
        ["tickets.sort"] = "Sort by",
        ["tickets.sort.newest"] = "Created (newest)",
        ["tickets.sort.oldest"] = "Created (oldest)",
        ["tickets.sort.updated"] = "Last updated",
        ["tickets.sort.priority"] = "Priority",
        ["tickets.sort.subject"] = "Subject (A-Z)",
        ["tickets.none"] = "No tickets found.",
        ["tickets.assigned"] = "Ticket assigned.",
        ["tickets.closed_success"] = "Ticket closed.",
        ["tickets.savefilter"] = "Save Filter",
        ["tickets.loadfilter"] = "Saved Filters",
        ["tickets.deletefilter"] = "Delete Filter",
        ["tickets.filtername"] = "Filter name",

        // Ticket Detail
        ["detail.description"] = "Description",
        ["detail.no_description"] = "No description.",
        ["detail.edit"] = "Edit ticket",
        ["detail.subject"] = "Subject",
        ["detail.status"] = "Status",
        ["detail.priority"] = "Priority",
        ["detail.creator"] = "Creator",
        ["detail.assignee"] = "Assignee",
        ["detail.unassigned"] = "Unassigned",
        ["detail.group"] = "Group",
        ["detail.type"] = "Type",
        ["detail.created"] = "Created",
        ["detail.updated"] = "Updated",
        ["detail.due"] = "Due",
        ["detail.assign_to"] = "Assign to",
        ["detail.assign"] = "Assign",
        ["detail.remove_assign"] = "Remove",
        ["detail.tags"] = "Tags",
        ["detail.linked"] = "Linked Tickets",
        ["detail.no_links"] = "No links.",
        ["detail.link_related"] = "Related",
        ["detail.link_blocks"] = "Blocks",
        ["detail.link_blocked_by"] = "Blocked by",
        ["detail.link_duplicate"] = "Duplicate",
        ["detail.subscribe"] = "Subscribe",
        ["detail.unsubscribe"] = "Unsubscribe",
        ["detail.subscribed"] = "Subscribed to ticket.",
        ["detail.unsubscribed"] = "Unsubscribed from ticket.",
        ["detail.delete_ticket"] = "Delete ticket",
        ["detail.not_found"] = "Ticket not found.",

        // Comments & Notes
        ["comments.title"] = "Comments",
        ["comments.new"] = "New comment",
        ["comments.send"] = "Send comment",
        ["comments.added"] = "Comment added.",
        ["comments.quickreply"] = "Insert quick reply",
        ["comments.mention"] = "Mention",
        ["notes.title"] = "Notes",
        ["notes.new"] = "New note",
        ["notes.add"] = "Add note",
        ["notes.added"] = "Note added.",

        // Attachments
        ["attachments.title"] = "Attachments",
        ["attachments.none"] = "No attachments.",
        ["attachments.upload"] = "Upload file",
        ["attachments.uploading"] = "Uploading...",
        ["attachments.uploaded"] = "File uploaded.",
        ["attachments.deleted"] = "Attachment deleted.",
        ["attachments.name"] = "Name",
        ["attachments.type"] = "Type",
        ["attachments.size"] = "Size",
        ["attachments.date"] = "Date",
        ["attachments.actions"] = "Actions",

        // Time Tracking
        ["time.title"] = "Time Tracking",
        ["time.timer"] = "Timer",
        ["time.start"] = "Start",
        ["time.stop"] = "Stop",
        ["time.description"] = "Description (optional)",
        ["time.total"] = "Total",
        ["time.no_entries"] = "No time entries.",
        ["time.col_start"] = "Start",
        ["time.col_duration"] = "Duration",
        ["time.col_description"] = "Description",

        // History
        ["history.title"] = "History",

        // Assets
        ["nav.assets"] = "Assets",
        ["assets.new"] = "New Asset",
        ["assets.name"] = "Name",
        ["assets.tag"] = "Asset Tag",
        ["assets.category"] = "Category",
        ["assets.location"] = "Location",
        ["assets.none"] = "No assets found.",
        ["assets.name_required"] = "Name is required.",
        ["assets.delete_confirm"] = "Really delete asset",

        // Recurring Tasks
        ["nav.recurring"] = "Recurring Tasks",
        ["recurring.new"] = "New Task",
        ["recurring.active"] = "Active",
        ["recurring.inactive"] = "Inactive",
        ["recurring.schedule"] = "Schedule",
        ["recurring.ticket_subject"] = "Ticket Subject",
        ["recurring.ticket_template"] = "Ticket Template",
        ["recurring.schedule_type"] = "Recurrence Type",
        ["recurring.monthly"] = "Monthly",
        ["recurring.quarterly"] = "Quarterly",
        ["recurring.annual"] = "Annual",
        ["recurring.day_of_month"] = "Day of Month",
        ["recurring.days_before"] = "Days before deadline",
        ["recurring.enabled"] = "Enabled",
        ["recurring.next_run"] = "Next Run",
        ["recurring.none"] = "No recurring tasks.",
        ["recurring.name_required"] = "Name is required.",
        ["recurring.delete_confirm"] = "Really delete task",

        // Reports
        ["reports.handover"] = "Handover Report",
        ["reports.sitzung"] = "Meeting Report",

        // Settings
        ["settings.title"] = "Settings",
        ["settings.appearance"] = "Appearance",
        ["settings.darkmode"] = "Dark Mode",
        ["settings.notifications"] = "Notifications",
        ["settings.notifications_desc"] = "Get notified about new tickets and changes",
        ["settings.notify_new"] = "New tickets",
        ["settings.notify_comments"] = "Comments",
        ["settings.notify_status"] = "Status changes",
        ["settings.notify_mine"] = "My tickets only",
        ["settings.notify_mine_desc"] = "Only for tickets assigned to me",
        ["settings.notify_highprio"] = "High priority only",
        ["settings.notify_highprio_desc"] = "Only for high/urgent priority",
        ["settings.language"] = "Language / Sprache",
        ["settings.api"] = "API Configuration",
        ["settings.api_url"] = "Server URL",
        ["settings.timeout"] = "Connection timeout (seconds)",
        ["settings.test_connection"] = "Test connection",
        ["settings.testing"] = "Testing...",
        ["settings.cache"] = "Offline Cache",
        ["settings.cache_enable"] = "Enable offline caching",
        ["settings.cache_desc"] = "Store tickets locally for offline access",
        ["settings.cache_clear"] = "Clear cache",
        ["settings.saved"] = "Settings saved.",
        ["settings.passkey"] = "Passkey / Biometrics",
        ["settings.passkey_register"] = "Register Passkey",
        ["settings.passkey_registered"] = "Passkey registered",
        ["settings.passkey_remove"] = "Remove Passkey",
        ["settings.passkey_desc"] = "Quick sign-in via fingerprint, face, or device PIN",
        ["settings.passkey_not_supported"] = "Your browser does not support passkeys.",
        ["settings.about"] = "About",

        // Accessibility
        ["a11y.toggle_menu"] = "Toggle menu",
        ["a11y.notifications"] = "Notifications",
        ["a11y.toggle_darkmode"] = "Toggle dark mode",
        ["a11y.logout"] = "Log out",
        ["a11y.main_navigation"] = "Main navigation",
    };
}
