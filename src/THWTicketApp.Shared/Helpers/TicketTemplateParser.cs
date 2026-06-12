using System.Text.Json;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// Parses the `{ ticketTemplates: [...] }` envelope of trudesk's v2
/// templates endpoint into <see cref="TicketTemplate"/> DTOs. Single
/// source of truth — replaces the page-local parsers that AddTicket and
/// Templates used to carry (RecurringTasks reached into AddTicket's).
/// </summary>
public static class TicketTemplateParser
{
    public static List<TicketTemplate> ParseTemplates(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ticketTemplates", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new();
            var list = new List<TicketTemplate>();
            foreach (var el in arr.EnumerateArray())
            {
                var typeRef = ExtractRef(el, "ticketType");
                var priorityRef = ExtractRef(el, "priority");
                list.Add(new TicketTemplate
                {
                    Id = el.TryGetProperty("_id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
                    Subject = el.TryGetProperty("subject", out var subjEl) ? subjEl.GetString() ?? "" : "",
                    Issue = el.TryGetProperty("issue", out var issueEl) ? issueEl.GetString() : null,
                    TypeId = typeRef?.Id,
                    TypeName = typeRef?.Name,
                    PriorityId = priorityRef?.Id,
                    PriorityName = priorityRef?.Name,
                    Checklist = ExtractChecklistTitles(el)
                });
            }
            return list;
        }
        catch
        {
            return new();
        }
    }

    // trudesk populiert Refs als Objekt mit _id + name, kann aber auch
    // ein einfacher ObjectId-String oder null sein. PopulatedRefConverter
    // (am Typ registriert) toleriert beide Formen.
    private static PopulatedRef? ExtractRef(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var el)) return null;
        return JsonSerializer.Deserialize<PopulatedRef>(el.GetRawText());
    }

    // Templates created before the checklist feature have no `checklist`
    // key — treat missing/non-array as empty. Blank titles are skipped.
    private static List<string> ExtractChecklistTitles(JsonElement parent)
    {
        var titles = new List<string>();
        if (!parent.TryGetProperty("checklist", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return titles;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
            {
                var title = titleEl.GetString();
                if (!string.IsNullOrWhiteSpace(title)) titles.Add(title);
            }
        }
        return titles;
    }
}
