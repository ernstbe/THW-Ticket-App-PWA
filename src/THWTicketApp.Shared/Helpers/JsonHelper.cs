using System.Text.Json;

namespace THWTicketApp.Shared.Helpers;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions DefaultOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Deserializes a JSON array that may be wrapped in a named property.
    /// Handles v2 format: { success, data: { propertyName: [...] } } or { success, data: [...] }
    /// and v1 format: { propertyName: [...] }
    /// </summary>
    public static T[] DeserializeWrappedArray<T>(string json, string propertyName, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // v2 response: check for { data: ... } wrapper first
        if (root.TryGetProperty("data", out var dataEl))
        {
            // data might be the array directly
            if (dataEl.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<T[]>(dataEl.GetRawText(), options) ?? [];
            // data might contain the named property: { data: { tickets: [...] } }
            if (dataEl.ValueKind == JsonValueKind.Object &&
                dataEl.TryGetProperty(propertyName, out var innerEl) && innerEl.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<T[]>(innerEl.GetRawText(), options) ?? [];
            // data might be a single object - wrap in array
            if (dataEl.ValueKind == JsonValueKind.Object)
                return [JsonSerializer.Deserialize<T>(dataEl.GetRawText(), options)!];
        }

        // v1 response: { propertyName: [...] }
        if (root.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<T[]>(el.GetRawText(), options) ?? [];

        // Fallback: try root as array
        if (root.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<T[]>(json, options) ?? [];

        return [];
    }
}
