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
                var (typeId, typeName) = ExtractRef(el, "ticketType");
                var (priorityId, priorityName) = ExtractRef(el, "priority");
                list.Add(new TicketTemplate
                {
                    Id = el.TryGetProperty("_id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
                    Subject = el.TryGetProperty("subject", out var subjEl) ? subjEl.GetString() ?? "" : "",
                    Issue = el.TryGetProperty("issue", out var issueEl) ? issueEl.GetString() : null,
                    TypeId = typeId,
                    TypeName = typeName,
                    PriorityId = priorityId,
                    PriorityName = priorityName,
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
    // ein einfacher ObjectId-String oder null sein. Beide Formen toleriert.
    private static (string? id, string? name) ExtractRef(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var el)) return (null, null);
        if (el.ValueKind == JsonValueKind.String) return (el.GetString(), null);
        if (el.ValueKind == JsonValueKind.Object)
        {
            var id = el.TryGetProperty("_id", out var idEl) ? idEl.GetString() : null;
            var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            return (id, name);
        }
        return (null, null);
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
