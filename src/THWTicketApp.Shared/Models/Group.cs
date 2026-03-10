using System.Text.Json;
using System.Text.Json.Serialization;

namespace THWTicketApp.Shared.Models;

public class Group
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Name { get; set; }
    [JsonIgnore]
    public List<Assignee> MembersList { get; set; } = [];
    [JsonPropertyName("members")]
    public JsonElement? RawMembers
    {
        get => null;
        set
        {
            if (value is { ValueKind: JsonValueKind.Array } arr)
            {
                MembersList = new List<Assignee>();
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.ValueKind == JsonValueKind.String)
                        MembersList.Add(new Assignee { Id = elem.GetString()! });
                    else if (elem.ValueKind == JsonValueKind.Object)
                    {
                        var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var member = JsonSerializer.Deserialize<Assignee>(elem.GetRawText(), opt);
                        if (member != null) MembersList.Add(member);
                    }
                }
            }
        }
    }
    [JsonIgnore]
    public List<string> SendMailToIds { get; set; } = [];
    [JsonPropertyName("sendMailTo")]
    public JsonElement? RawSendMailTo
    {
        get => null;
        set
        {
            if (value is { ValueKind: JsonValueKind.Array } arr)
            {
                SendMailToIds = new List<string>();
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.ValueKind == JsonValueKind.String)
                        SendMailToIds.Add(elem.GetString()!);
                    else if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("_id", out var id))
                        SendMailToIds.Add(id.GetString()!);
                }
            }
        }
    }
    public bool Public { get; set; }
    [JsonPropertyName("__v")]
    public int Version { get; set; }
}
