using System.Text.Json;

namespace THWTicketApp.Shared.Helpers;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions DefaultOptions = new() { PropertyNameCaseInsensitive = true };

    public static T[] DeserializeWrappedArray<T>(string json, string propertyName, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<T[]>(el.GetRawText(), options) ?? [];
        return JsonSerializer.Deserialize<T[]>(json, options) ?? [];
    }
}
