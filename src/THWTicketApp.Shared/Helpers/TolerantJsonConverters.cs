using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Shared.Helpers;

// trudesk can deliver `null` (or otherwise off-type values) in fields the
// client models as non-nullable value types — e.g. `"dueDate": null` after a
// due date is cleared (the shared server update helper stores the null
// explicitly). Default System.Text.Json then throws while deserializing the
// element, which used to abort the ENTIRE ticket list and surface
// "Ungültiges Datenformat vom Server." on every page. These converters map
// such values onto the defaults the codebase already treats as "unset"
// (DateTime.MinValue / 0 / false) instead of throwing.

public sealed class TolerantDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                if (reader.TryGetDateTime(out var dt)) return dt;
                var s = reader.GetString();
                return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : DateTime.MinValue;
            case JsonTokenType.Null:
                return DateTime.MinValue;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                reader.Skip();
                return DateTime.MinValue;
            default:
                return DateTime.MinValue;
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

public sealed class TolerantNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                if (reader.TryGetDateTime(out var dt)) return dt;
                var s = reader.GetString();
                return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : null;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                reader.Skip();
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(value.Value);
        else writer.WriteNullValue();
    }
}

public sealed class TolerantIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var i)) return i;
                // e.g. 42.0 from Mongo doubles — truncate instead of throwing.
                var d = reader.GetDouble();
                return d >= int.MinValue && d <= int.MaxValue ? (int)d : 0;
            case JsonTokenType.String:
                var s = reader.GetString();
                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            case JsonTokenType.Null:
                return 0;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                reader.Skip();
                return 0;
            default:
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

public sealed class TolerantBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
                return bool.TryParse(reader.GetString(), out var parsed) && parsed;
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var i) ? i != 0 : reader.GetDouble() != 0;
            case JsonTokenType.Null:
                return false;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                reader.Skip();
                return false;
            default:
                return false;
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}

// trudesk .populate()s some ref arrays only partially — a deleted or
// otherwise unresolved member is left as the bare ObjectId string instead of
// the populated object (seen on `subscribers`, PR #167's absorber-removal
// investigation). Each element is either a populated object (deserialize
// normally) or a bare string (wrap as an Assignee with just Id set) so the
// whole array doesn't throw and abort the ticket.
public sealed class TolerantAssigneeListConverter : JsonConverter<List<Assignee>>
{
    // Reference-type converters aren't invoked for a JSON null by default —
    // System.Text.Json assigns null directly instead, which would stomp the
    // property's `= new()` initializer. Opt in so Read() below runs and
    // returns an empty list instead.
    public override bool HandleNull => true;

    public override List<Assignee> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<Assignee>();
        if (reader.TokenType == JsonTokenType.Null) return list;
        if (reader.TokenType != JsonTokenType.StartArray) { reader.Skip(); return list; }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(new Assignee { Id = reader.GetString() ?? string.Empty });
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                var assignee = JsonSerializer.Deserialize<Assignee>(ref reader, options);
                if (assignee != null) list.Add(assignee);
            }
            else
            {
                reader.Skip();
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<Assignee> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var a in value) JsonSerializer.Serialize(writer, a, options);
        writer.WriteEndArray();
    }
}
