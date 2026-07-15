using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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
