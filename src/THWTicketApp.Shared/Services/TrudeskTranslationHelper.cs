using System.Text.RegularExpressions;

namespace THWTicketApp.Shared.Services;

public static class TrudeskTranslationHelper
{
    // Trudesk stores notification titles as fixed English strings for some
    // events (Created/Comment/Mention) and German for others (assignment), so
    // the list reads as mixed language with an inconsistent "Ticket#" spacing
    // (frontend review ISSUE-5). Normalize the English ones to German and to a
    // consistent "Ticket #<n>" form; already-German titles pass through.
    private static readonly (Regex Pattern, string Replacement)[] NotificationTitleRules =
    {
        (new Regex(@"^Ticket\s*#\s*(\d+)\s+Created$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Ticket #$1 erstellt"),
        (new Regex(@"^Comment Added to Ticket\s*#\s*(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Neuer Kommentar zu Ticket #$1"),
        (new Regex(@"^You were mentioned in Ticket\s*#\s*(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Du wurdest in Ticket #$1 erwähnt"),
    };

    /// <summary>
    /// Translate a trudesk notification title to German and normalize spacing.
    /// Unknown or already-German titles are returned unchanged.
    /// </summary>
    public static string? TranslateNotificationTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return title;
        var trimmed = title.Trim();
        foreach (var (pattern, replacement) in NotificationTitleRules)
        {
            if (pattern.IsMatch(trimmed))
                return pattern.Replace(trimmed, replacement);
        }
        return title;
    }

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

    // Trudesk speichert History-Descriptions auf Englisch (z.B. "Ticket Group
    // set to: OV Stab"). Da der Action-Label oben („Gruppe geändert") die
    // Aktion bereits benennt, ist das Verb redundant — wir extrahieren nur
    // den neuen Wert. Für komplett redundante Descriptions ("Ticket was
    // created.") wird null zurückgegeben, sodass die UI die Body-Box weglässt.
    public static string? TranslateHistoryDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        // Trivial descriptions that just restate the action label.
        var trimmed = description.Trim();
        if (trimmed.Equals("Ticket was created.", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Equals("Ticket batch-deleted", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Equals("Ticket metadata was updated", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Equals("Ticket Issue was updated.", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Equals("Ticket Subject was updated.", StringComparison.OrdinalIgnoreCase)) return null;

        // "Ticket X set to: Y" → strip the "Ticket X set to:" prefix and
        // keep just the value. Action label above already names X.
        var colon = trimmed.IndexOf(':');
        if (colon > 0 && colon < trimmed.Length - 1)
        {
            var prefix = trimmed[..colon].TrimEnd();
            if (prefix.StartsWith("Ticket ", StringComparison.OrdinalIgnoreCase)
                && prefix.EndsWith(" set to", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[(colon + 1)..].Trim();
                // Value can be a status OR priority ("High"→"Hoch") — übersetzen.
                return TranslateStatusOrPriority(value);
            }
        }

        // "assignee set to: X" / "status set to: X" / "priority set to: X" —
        // v2 batch updates use a slightly different wording (no leading "Ticket").
        if (trimmed.Contains(" set to: ", StringComparison.OrdinalIgnoreCase))
        {
            var idx = trimmed.IndexOf(" set to: ", StringComparison.OrdinalIgnoreCase);
            var value = trimmed[(idx + " set to: ".Length)..].Trim();
            return TranslateStatusOrPriority(value);
        }

        return description;
    }

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
                return $"{prefixTranslated}: {TranslateStatusOrPriority(suffix)}";
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

    // History rows carry the new VALUE of a changed field but not always which
    // field it was, so a priority change (High/Critical/…) used to be run through
    // TranslateStatus and stayed English (#222). Priority and status names don't
    // overlap, so try both dictionaries.
    public static string TranslateStatusOrPriority(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (PriorityTranslations.TryGetValue(value, out var p)) return p;
        if (StatusTranslations.TryGetValue(value, out var s)) return s;
        return value;
    }

    public static void TranslateTicket(THWTicketApp.Shared.Models.Ticket ticket)
    {
        if (ticket.Status != null && !string.IsNullOrEmpty(ticket.Status.Name))
            ticket.Status.Name = TranslateStatus(ticket.Status.Name);
        if (ticket.Priority != null && !string.IsNullOrEmpty(ticket.Priority.Name))
            ticket.Priority.Name = TranslatePriority(ticket.Priority.Name);
    }
}
