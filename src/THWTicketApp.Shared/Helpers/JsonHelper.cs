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

        // Fallback: try root as array first (before property access)
        if (root.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<T[]>(json, options) ?? [];

        // Anything that isn't an object (e.g. a bare `null` payload) has no
        // properties to inspect — TryGetProperty would throw. Bail out empty.
        if (root.ValueKind != JsonValueKind.Object)
            return [];

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

        return [];
    }

    /// <summary>
    /// Like <see cref="DeserializeWrappedArray{T}(string, string, JsonSerializerOptions?)"/>
    /// but tolerates two possible wrapper keys, trying each in order. Used for
    /// endpoints whose v1 and v2 shapes differ only by the wrapper name
    /// (e.g. v1 { users: [...] } vs v2 { accounts: [...] }, or
    /// v1 { types: [...] } vs v2 { ticketTypes: [...] }). The first key that
    /// yields a non-empty array wins; otherwise an empty array is returned.
    /// </summary>
    public static T[] DeserializeWrappedArray<T>(string json, string primaryProperty, string fallbackProperty, JsonSerializerOptions? options = null)
    {
        var result = DeserializeWrappedArray<T>(json, primaryProperty, options);
        if (result.Length > 0) return result;
        return DeserializeWrappedArray<T>(json, fallbackProperty, options);
    }
}
