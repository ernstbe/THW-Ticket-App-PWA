using System.Text.Json;
using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

/// <summary>
/// A reference field that trudesk may return either populated as an object
/// ({_id, name, ...}) or as a bare ObjectId string. The converter accepts
/// both shapes on read and writes the id back as a JSON string, so a
/// serialize → deserialize roundtrip keeps the id (the name is a
/// read-only convenience and is intentionally not written).
/// </summary>
[JsonConverter(typeof(PopulatedRefConverter))]
public sealed record PopulatedRef(string? Id, string? Name);

public sealed class PopulatedRefConverter : JsonConverter<PopulatedRef>
{
    public override PopulatedRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new PopulatedRef(reader.GetString(), null);

            case JsonTokenType.StartObject:
                string? id = null;
                string? name = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    if (propertyName == "_id" && reader.TokenType == JsonTokenType.String)
                        id = reader.GetString();
                    else if (propertyName == "name" && reader.TokenType == JsonTokenType.String)
                        name = reader.GetString();
                    else
                        reader.Skip();
                }
                return new PopulatedRef(id, name);

            default:
                // Tolerate unexpected token types (numbers, booleans, arrays)
                // the same way the old JsonElement absorber did: yield null.
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, PopulatedRef value, JsonSerializerOptions options)
    {
        if (value.Id is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Id);
    }
}
