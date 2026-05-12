namespace THWTicketApp.Shared.Services;

public static class TrudeskTranslationHelper
{
    private static readonly Dictionary<string, string> PriorityTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Normal"] = "Normal",
        ["Low"] = "Niedrig",
        ["Medium"] = "Mittel",
        ["High"] = "Hoch",
        ["Urgent"] = "Dringend",
        ["Critical"] = "Kritisch"
    };

    private static readonly Dictionary<string, string> StatusTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["New"] = "Neu",
        ["Open"] = "Offen",
        ["Pending"] = "Ausstehend",
        ["Closed"] = "Geschlossen",
        ["In Progress"] = "In Bearbeitung",
        ["On Hold"] = "Wartend",
        ["Resolved"] = "Gelöst"
    };

    private static readonly Dictionary<string, string> HistoryActionTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        // trudesk-Backend (src/models/ticket.js + src/controllers/api/*) emittiert
        // diese Action-Strings. Beim Hinzufügen einer neuen Aktion im Backend
        // hier mit pflegen, sonst zeigt das Activity-Feed den rohen Key an.
        ["ticket:created"] = "Ticket erstellt",
        ["ticket:updated"] = "Ticket aktualisiert",
        ["ticket:deleted"] = "Ticket gelöscht",
        ["ticket:set:status"] = "Status geändert",
        ["ticket:set:assignee"] = "Zugewiesen",
        ["ticket:set:type"] = "Typ geändert",
        ["ticket:set:priority"] = "Priorität geändert",
        ["ticket:set:group"] = "Gruppe geändert",
        ["ticket:set:duedate"] = "Fälligkeitsdatum gesetzt",
        ["ticket:update:issue"] = "Beschreibung geändert",
        ["ticket:update:subject"] = "Betreff geändert",
        ["ticket:update:metadata"] = "Metadaten geändert",
        ["ticket:checklist:add"] = "Checkliste-Eintrag hinzugefügt",
        ["ticket:checklist:update"] = "Checkliste-Eintrag aktualisiert",
        ["ticket:checklist:remove"] = "Checkliste-Eintrag entfernt",
        ["ticket:comment:added"] = "Kommentar hinzugefügt",
        ["ticket:comment:updated"] = "Kommentar bearbeitet",
        ["ticket:delete:comment"] = "Kommentar gelöscht",
        ["ticket:note:added"] = "Notiz hinzugefügt",
        ["ticket:note:updated"] = "Notiz bearbeitet",
        ["ticket:delete:note"] = "Notiz gelöscht",
        ["ticket:added:attachment"] = "Anhang hinzugefügt",
        ["ticket:delete:attachment"] = "Anhang entfernt",
        ["ticket:subscriber:added"] = "Abonnent hinzugefügt",
        ["ticket:subscriber:removed"] = "Abonnent entfernt",

        // Legacy-Keys — kommen in alten History-Einträgen vor, neue Tickets nutzen
        // die :set:/:update:-Keys oben. Aliase damit alte Tickets sauber bleiben.
        ["ticket:status:updated"] = "Status geändert",
        ["ticket:priority:updated"] = "Priorität geändert",
        ["ticket:group:updated"] = "Gruppe geändert",
        ["ticket:type:updated"] = "Typ geändert",
        ["ticket:assignee:cleared"] = "Zuweisung entfernt",
        ["ticket:attachment:added"] = "Anhang hinzugefügt",
        ["ticket:attachment:removed"] = "Anhang entfernt",
    };

    public static string TranslateHistoryAction(string? action)
    {
        if (string.IsNullOrEmpty(action)) return action ?? string.Empty;
        if (HistoryActionTranslations.TryGetValue(action, out var translated)) return translated;

        // Dynamische Action-Strings wie "ticket:set:status:Open" — Suffix abtrennen,
        // Basis übersetzen und Status-Name am Ende anhängen.
        var lastColon = action.LastIndexOf(':');
        if (lastColon > 0)
        {
            var prefix = action[..lastColon];
            var suffix = action[(lastColon + 1)..];
            if (HistoryActionTranslations.TryGetValue(prefix, out var prefixTranslated))
                return $"{prefixTranslated}: {TranslateStatus(suffix)}";
        }
        return action;
    }

    public static string TranslatePriority(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? string.Empty;
        return PriorityTranslations.TryGetValue(name, out var translated) ? translated : name;
    }

    public static string TranslateStatus(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? string.Empty;
        return StatusTranslations.TryGetValue(name, out var translated) ? translated : name;
    }

    public static void TranslateTicket(THWTicketApp.Shared.Models.Ticket ticket)
    {
        if (ticket.Status != null && !string.IsNullOrEmpty(ticket.Status.Name))
            ticket.Status.Name = TranslateStatus(ticket.Status.Name);
        if (ticket.Priority != null && !string.IsNullOrEmpty(ticket.Priority.Name))
            ticket.Priority.Name = TranslatePriority(ticket.Priority.Name);
    }
}
