using System.Text.Json;

namespace THWTicketApp.Shared.Helpers;

public static class JsonHelper
{
    /// <summary>
    /// Parse options used for all server payloads: case-insensitive property
    /// matching plus the tolerant value-type converters, so a single off-type
    /// field (e.g. <c>"dueDate": null</c> after a cleared due date) maps to the
    /// established "unset" defaults instead of throwing and taking the whole
    /// response down with it.
    /// </summary>
    public static readonly JsonSerializerOptions TolerantOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new TolerantDateTimeConverter(),
            new TolerantNullableDateTimeConverter(),
            new TolerantIntConverter(),
            new TolerantBoolConverter()
        }
    };

    /// <summary>
    /// Deserializes a JSON array that may be wrapped in a named property.
    /// Handles v2 format: { success, data: { propertyName: [...] } } or { success, data: [...] }
    /// and v1 format: { propertyName: [...] }.
    /// Array elements are deserialized individually: an element the client
    /// cannot parse even with the tolerant converters is skipped (with a
    /// console warning) instead of aborting the entire list — one broken
    /// ticket must never blank every page of the app.
    /// </summary>
    public static T[] DeserializeWrappedArray<T>(string json, string propertyName, JsonSerializerOptions? options = null)
    {
        options = EnsureTolerant(options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Fallback: try root as array first (before property access)
        if (root.ValueKind == JsonValueKind.Array)
            return DeserializeArrayResilient<T>(root, options);

        // Anything that isn't an object (e.g. a bare `null` payload) has no
        // properties to inspect — TryGetProperty would throw. Bail out empty.
        if (root.ValueKind != JsonValueKind.Object)
            return [];

        // v2 response: check for { data: ... } wrapper first
        if (root.TryGetProperty("data", out var dataEl))
        {
            // data might be the array directly
            if (dataEl.ValueKind == JsonValueKind.Array)
                return DeserializeArrayResilient<T>(dataEl, options);
            // data might contain the named property: { data: { tickets: [...] } }
            if (dataEl.ValueKind == JsonValueKind.Object &&
                dataEl.TryGetProperty(propertyName, out var innerEl) && innerEl.ValueKind == JsonValueKind.Array)
                return DeserializeArrayResilient<T>(innerEl, options);
            // data might be a single object - wrap in array
            if (dataEl.ValueKind == JsonValueKind.Object)
                return [JsonSerializer.Deserialize<T>(dataEl.GetRawText(), options)!];
        }

        // v1 response: { propertyName: [...] }
        if (root.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.Array)
            return DeserializeArrayResilient<T>(el, options);

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

    /// <summary>
    /// Returns the raw JSON text of a wrapped array (same v1/v2 shapes as
    /// <see cref="DeserializeWrappedArray{T}(string, string, JsonSerializerOptions?)"/>)
    /// without deserializing the elements — used to hand the untouched server ticket
    /// array to the offline cache. Returns <c>null</c> when no array is found or the
    /// input isn't valid JSON.
    /// </summary>
    public static string? ExtractWrappedArrayRaw(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
                return root.GetRawText();
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.ValueKind == JsonValueKind.Array)
                    return dataEl.GetRawText();
                if (dataEl.ValueKind == JsonValueKind.Object &&
                    dataEl.TryGetProperty(propertyName, out var innerEl) && innerEl.ValueKind == JsonValueKind.Array)
                    return innerEl.GetRawText();
            }

            if (root.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.Array)
                return el.GetRawText();

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Callers construct their own options all over the pages; make sure every
    // parse path gets the tolerant converters regardless, without mutating the
    // caller's instance (JsonSerializerOptions are frozen after first use).
    private static JsonSerializerOptions EnsureTolerant(JsonSerializerOptions? options)
    {
        if (options is null || ReferenceEquals(options, TolerantOptions))
            return TolerantOptions;
        foreach (var converter in options.Converters)
        {
            if (converter is TolerantDateTimeConverter)
                return options;
        }
        var merged = new JsonSerializerOptions(options);
        merged.Converters.Add(new TolerantDateTimeConverter());
        merged.Converters.Add(new TolerantNullableDateTimeConverter());
        merged.Converters.Add(new TolerantIntConverter());
        merged.Converters.Add(new TolerantBoolConverter());
        return merged;
    }

    // Element-wise deserialization: a single element that still fails (e.g. a
    // structurally alien entry) is dropped instead of poisoning the whole
    // array. JSON nulls inside the array are dropped too — downstream code
    // never expects null tickets/statuses in a list.
    private static T[] DeserializeArrayResilient<T>(JsonElement arrayEl, JsonSerializerOptions options)
    {
        var items = new List<T>(arrayEl.GetArrayLength());
        var skipped = 0;
        foreach (var el in arrayEl.EnumerateArray())
        {
            try
            {
                var item = el.Deserialize<T>(options);
                if (item is not null)
                    items.Add(item);
            }
            catch (JsonException)
            {
                skipped++;
            }
        }
        if (skipped > 0)
            Console.Error.WriteLine($"[JsonHelper] Skipped {skipped} of {items.Count + skipped} {typeof(T).Name} entries the client could not parse.");
        return [.. items];
    }
}
