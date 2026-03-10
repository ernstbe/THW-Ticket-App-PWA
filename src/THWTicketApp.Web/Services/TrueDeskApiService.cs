using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class TrueDeskApiService : ITrueDeskApiService
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly LocalStorageService _localStorage;
    private string? _authToken;

    public string? CurrentUsername { get; private set; }
    public string? CurrentUserId { get; private set; }
    public string? LastError { get; private set; }

    public TrueDeskApiService(HttpClient httpClient, AppSettings settings, LocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _settings = settings;
        _localStorage = localStorage;
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.ConnectionTimeoutSeconds);
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);

    private void SetAuthHeader(string? token)
    {
        if (_httpClient.DefaultRequestHeaders.Contains("accesstoken"))
            _httpClient.DefaultRequestHeaders.Remove("accesstoken");
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Add("accesstoken", token);
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            var payload = new { username, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/login", content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                _authToken = doc.RootElement.GetProperty("accessToken").GetString();
                SetAuthHeader(_authToken);

                if (doc.RootElement.TryGetProperty("user", out var userEl) &&
                    userEl.TryGetProperty("_id", out var idEl))
                {
                    CurrentUserId = idEl.GetString();
                }

                CurrentUsername = username;
                await _localStorage.SetItemAsync("auth_token", _authToken ?? string.Empty);
                await _localStorage.SetItemAsync("auth_username", username);
                await _localStorage.SetItemAsync("auth_userid", CurrentUserId ?? string.Empty);
                return true;
            }
            return false;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
        catch (JsonException) { return false; }
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var storedToken = await _localStorage.GetItemAsync("auth_token");
            if (!string.IsNullOrEmpty(storedToken))
            {
                _authToken = storedToken;
                CurrentUsername = await _localStorage.GetItemAsync("auth_username");
                CurrentUserId = await _localStorage.GetItemAsync("auth_userid");
                SetAuthHeader(_authToken);

                try
                {
                    var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/login");
                    if (!response.IsSuccessStatusCode)
                    {
                        _authToken = null;
                        CurrentUsername = null;
                        CurrentUserId = null;
                        SetAuthHeader(null);
                        await _localStorage.RemoveItemAsync("auth_token");
                        await _localStorage.RemoveItemAsync("auth_username");
                        await _localStorage.RemoveItemAsync("auth_userid");
                        return false;
                    }
                }
                catch
                {
                    // Network error - assume token is valid
                }

                return true;
            }
        }
        catch
        {
            // localStorage not available or error reading
        }
        return false;
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (IsAuthenticated)
                await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/logout");
        }
        catch { }

        _authToken = null;
        CurrentUsername = null;
        CurrentUserId = null;
        SetAuthHeader(null);
        await _localStorage.RemoveItemAsync("auth_token");
        await _localStorage.RemoveItemAsync("auth_username");
        await _localStorage.RemoveItemAsync("auth_userid");
    }

    public async Task<string> GetTicketsAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets?limit=1000");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketsPagedAsync(int page = 0, int limit = 50)
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets?page={page}&limit={limit}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketsFilteredAsync(string? status = null, bool? assignedSelf = null, int limit = 1000)
    {
        var queryParts = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrEmpty(status))
            queryParts.Add($"status={Uri.EscapeDataString(status)}");
        if (assignedSelf == true)
            queryParts.Add("assignedself=true");
        var query = string.Join("&", queryParts);
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets?{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> SearchTicketsAsync(string query)
    {
        var encoded = Uri.EscapeDataString(query);
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/search?search={encoded}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketAsync(string ticketUid)
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/{ticketUid}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> AddTicketAsync(string title, string description, string? assigneeId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        var payload = new Dictionary<string, object?>
        {
            ["subject"] = title,
            ["issue"] = description,
            ["owner"] = CurrentUserId
        };
        if (!string.IsNullOrEmpty(assigneeId))
            payload["assignee"] = assigneeId;

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/tickets/create", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return false;

        var payload = new Dictionary<string, object?>
        {
            ["subject"] = subject,
            ["issue"] = issue ?? string.Empty,
            ["owner"] = CurrentUserId,
        };

        if (!string.IsNullOrEmpty(typeId)) payload["type"] = typeId;
        if (!string.IsNullOrEmpty(priorityId)) payload["priority"] = priorityId;
        if (!string.IsNullOrEmpty(groupId)) payload["group"] = groupId;
        if (!string.IsNullOrEmpty(assigneeId)) payload["assignee"] = assigneeId;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/tickets/create", content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            LastError = $"{(int)response.StatusCode}: {body}";
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> EditTicketAsync(Ticket ticket)
    {
        if (ticket == null || string.IsNullOrWhiteSpace(ticket.Id))
            return false;

        var payload = new Dictionary<string, object?>();
        if (ticket.Subject != null) payload["subject"] = ticket.Subject;
        if (ticket.Issue != null) payload["issue"] = ticket.Issue;
        if (ticket.Priority?.Id != null) payload["priority"] = ticket.Priority.Id;
        if (ticket.Status?.Id != null) payload["status"] = ticket.Status.Id;

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{_settings.ApiBaseUrl}/tickets/{ticket.Id}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteTicketAsync(string ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return false;
        var response = await _httpClient.DeleteAsync($"{_settings.ApiBaseUrl}/tickets/{ticketId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateTicketStatusAsync(string ticketId, string statusId)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(statusId))
            return false;

        var payload = new { status = statusId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{_settings.ApiBaseUrl}/tickets/{ticketId}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AssignTicketAsync(string ticketId, string userId)
    {
        var payload = new { assignee = userId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{_settings.ApiBaseUrl}/tickets/{ticketId}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearTicketAssigneeAsync(string ticketId)
    {
        var payload = new { assignee = (string?)null };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{_settings.ApiBaseUrl}/tickets/{ticketId}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddCommentAsync(string id, string ownerId, string newComment)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newComment))
            return false;

        var payload = new { _id = id, ownerId, comment = newComment };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/tickets/addcomment", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddNoteAsync(string ticketId, string ownerId, string note)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(note))
            return false;

        var payload = new { ticketid = ticketId, owner = ownerId, note };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/tickets/addnote", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UploadAttachmentAsync(string ticketId, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(ticketId), "ticketId");

        var baseUrl = _settings.ApiBaseUrl.Replace("/api/v1", "");
        var response = await _httpClient.PostAsync($"{baseUrl}/tickets/uploadattachment", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<Stream?> DownloadAttachmentAsync(string attachmentPath)
    {
        var baseUrl = _settings.ApiBaseUrl.Replace("/api/v1", "");
        var response = await _httpClient.GetAsync($"{baseUrl}{attachmentPath}");
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadAsStreamAsync();
        return null;
    }

    public string GetAttachmentUrl(string attachmentPath)
    {
        var baseUrl = _settings.ApiBaseUrl.Replace("/api/v1", "");
        return $"{baseUrl}{attachmentPath}";
    }

    public async Task<bool> DeleteAttachmentAsync(string ticketId, string attachmentId)
    {
        var response = await _httpClient.DeleteAsync(
            $"{_settings.ApiBaseUrl}/tickets/{ticketId}/attachments/remove/{attachmentId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<string> GetStatusesAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetUsersAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/users");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAssigneesAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/users/getassignees");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketTypesAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/types");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetPrioritiesAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/priorities");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTagsAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tags/limit");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetGroupsAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/groups");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketsByGroupAsync(string groupId, int page = 0, int limit = 50)
    {
        var response = await _httpClient.GetAsync(
            $"{_settings.ApiBaseUrl}/tickets/group/{groupId}?page={page}&limit={limit}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetOverdueTicketsAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/overdue");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> SubscribeToTicketAsync(string ticketId, bool subscribe)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrEmpty(CurrentUserId))
            return false;

        var payload = new { user = CurrentUserId, subscribe };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{_settings.ApiBaseUrl}/tickets/{ticketId}/subscribe", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<string> GetNotificationsAsync()
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/users/notifications");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<int> GetNotificationCountAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/users/notificationCount");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("count", out var countEl))
            {
                if (countEl.ValueKind == JsonValueKind.String)
                    return int.TryParse(countEl.GetString(), out var c) ? c : 0;
                return countEl.GetInt32();
            }
            return 0;
        }
        catch { return 0; }
    }

    public async Task<string> GetTicketStatsAsync(int timespan = 30)
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/stats/{timespan}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForGroupAsync(string groupId)
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/stats/group/{groupId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForUserAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/tickets/stats/user/{userId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
