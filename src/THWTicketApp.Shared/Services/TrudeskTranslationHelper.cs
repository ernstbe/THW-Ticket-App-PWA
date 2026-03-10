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
        ["ticket:created"] = "Ticket erstellt",
        ["ticket:updated"] = "Ticket aktualisiert",
        ["ticket:deleted"] = "Ticket gelöscht",
        ["ticket:status:updated"] = "Status geändert",
        ["ticket:priority:updated"] = "Priorität geändert",
        ["ticket:group:updated"] = "Gruppe geändert",
        ["ticket:type:updated"] = "Typ geändert",
        ["ticket:assignee:set"] = "Zugewiesen",
        ["ticket:assignee:cleared"] = "Zuweisung entfernt",
        ["ticket:comment:added"] = "Kommentar hinzugefügt",
        ["ticket:note:added"] = "Notiz hinzugefügt",
        ["ticket:attachment:added"] = "Anhang hinzugefügt",
        ["ticket:attachment:removed"] = "Anhang entfernt",
        ["ticket:subscriber:added"] = "Abonnent hinzugefügt",
        ["ticket:subscriber:removed"] = "Abonnent entfernt",
    };

    public static string TranslateHistoryAction(string? action)
    {
        if (string.IsNullOrEmpty(action)) return action ?? string.Empty;
        return HistoryActionTranslations.TryGetValue(action, out var translated) ? translated : action;
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
