using System.Net.Http.Json;
using System.Net.Http.Headers;
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
    private string? _refreshToken;

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

    private string BaseUrl => _settings.ApiBaseUrl.TrimEnd('/');
    private string ServerUrl => BaseUrl.Replace("/api/v2", "").Replace("/api/v1", "");
    private bool IsV2 => BaseUrl.Contains("/api/v2");
    // Some endpoints only exist in v1 - use this for those calls
    private string V1BaseUrl => ServerUrl + "/api/v1";

    private void SetAuthHeader(string? token)
    {
        // Remove old headers
        _httpClient.DefaultRequestHeaders.Authorization = null;
        if (_httpClient.DefaultRequestHeaders.Contains("accesstoken"))
            _httpClient.DefaultRequestHeaders.Remove("accesstoken");

        if (!string.IsNullOrEmpty(token))
        {
            if (IsV2)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else
                _httpClient.DefaultRequestHeaders.Add("accesstoken", token);
        }
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            var payload = new { username, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/login", content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                if (IsV2)
                {
                    // v2 returns { token, refreshToken }
                    _authToken = doc.RootElement.TryGetProperty("token", out var tokenEl)
                        ? tokenEl.GetString() : null;
                    _refreshToken = doc.RootElement.TryGetProperty("refreshToken", out var rtEl)
                        ? rtEl.GetString() : null;
                }
                else
                {
                    // v1 returns { accessToken }
                    _authToken = doc.RootElement.TryGetProperty("accessToken", out var atEl)
                        ? atEl.GetString() : null;
                }

                SetAuthHeader(_authToken);

                if (doc.RootElement.TryGetProperty("user", out var userEl) &&
                    userEl.TryGetProperty("_id", out var idEl))
                {
                    CurrentUserId = idEl.GetString();
                }

                CurrentUsername = username;
                await _localStorage.SetItemAsync("auth_token", _authToken ?? string.Empty);
                await _localStorage.SetItemAsync("auth_refresh_token", _refreshToken ?? string.Empty);
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
                _refreshToken = await _localStorage.GetItemAsync("auth_refresh_token");
                CurrentUsername = await _localStorage.GetItemAsync("auth_username");
                CurrentUserId = await _localStorage.GetItemAsync("auth_userid");
                SetAuthHeader(_authToken);

                try
                {
                    var response = await _httpClient.GetAsync($"{BaseUrl}/login");
                    if (!response.IsSuccessStatusCode)
                    {
                        // Try token refresh for v2
                        if (IsV2 && !string.IsNullOrEmpty(_refreshToken))
                        {
                            if (await TryRefreshTokenAsync())
                                return true;
                        }

                        await ClearAuthState();
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
        catch { }
        return false;
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        try
        {
            var payload = new { refreshToken = _refreshToken };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/token", content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                _authToken = doc.RootElement.TryGetProperty("token", out var tokenEl)
                    ? tokenEl.GetString() : null;
                SetAuthHeader(_authToken);
                await _localStorage.SetItemAsync("auth_token", _authToken ?? string.Empty);
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (IsAuthenticated)
                await _httpClient.GetAsync($"{BaseUrl}/logout");
        }
        catch { }

        await ClearAuthState();
    }

    private async Task ClearAuthState()
    {
        _authToken = null;
        _refreshToken = null;
        CurrentUsername = null;
        CurrentUserId = null;
        SetAuthHeader(null);
        await _localStorage.RemoveItemAsync("auth_token");
        await _localStorage.RemoveItemAsync("auth_refresh_token");
        await _localStorage.RemoveItemAsync("auth_username");
        await _localStorage.RemoveItemAsync("auth_userid");
    }

    // Tickets
    public async Task<string> GetTicketsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets?limit=1000"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketsPagedAsync(int page = 0, int limit = 50)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets?page={page}&limit={limit}"));
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
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets?{query}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> SearchTicketsAsync(string query)
    {
        var encoded = Uri.EscapeDataString(query);
        // v2 uses Elasticsearch endpoint (with MongoDB fallback), v1 uses ticket search
        if (IsV2)
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/es/search?search={encoded}"));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets/search?search={encoded}"));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    public async Task<string> GetTicketAsync(string ticketUid)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets/{ticketUid}"));
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
        // v1 uses /tickets/create, v2 uses /tickets
        var endpoint = IsV2 ? $"{BaseUrl}/tickets" : $"{BaseUrl}/tickets/create";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync(endpoint, content));
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
        // v1 uses /tickets/create, v2 uses /tickets
        var endpoint = IsV2 ? $"{BaseUrl}/tickets" : $"{BaseUrl}/tickets/create";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync(endpoint, content));
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

        var ticketData = new Dictionary<string, object?>();
        if (ticket.Subject != null) ticketData["subject"] = ticket.Subject;
        if (ticket.Issue != null) ticketData["issue"] = ticket.Issue;
        if (ticket.Priority?.Id != null) ticketData["priority"] = ticket.Priority.Id;
        if (ticket.Status?.Id != null) ticketData["status"] = ticket.Status.Id;

        // v2 expects { ticket: {...} } wrapper, v1 expects flat object
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        // v2 uses uid, v1 uses _id
        var identifier = IsV2 ? ticket.Uid.ToString() : ticket.Id;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{identifier}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteTicketAsync(string ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return false;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{BaseUrl}/tickets/{ticketId}"));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateTicketStatusAsync(string ticketId, string statusId)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(statusId))
            return false;

        var ticketData = new Dictionary<string, object?> { ["status"] = statusId };
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{ticketId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AssignTicketAsync(string ticketId, string userId)
    {
        var ticketData = new Dictionary<string, object?> { ["assignee"] = userId };
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{ticketId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearTicketAssigneeAsync(string ticketId)
    {
        var ticketData = new Dictionary<string, object?> { ["assignee"] = null };
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{ticketId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddCommentAsync(string id, string ownerId, string newComment)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newComment))
            return false;

        var payload = new { _id = id, ownerId, comment = newComment };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        // addcomment only exists in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V1BaseUrl}/tickets/addcomment", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddNoteAsync(string ticketId, string ownerId, string note)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(note))
            return false;

        var payload = new { ticketid = ticketId, owner = ownerId, note };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        // addnote only exists in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V1BaseUrl}/tickets/addnote", content));
        return response.IsSuccessStatusCode;
    }

    // Attachments
    public async Task<bool> UploadAttachmentAsync(string ticketId, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(ticketId), "ticketId");
        content.Add(new StringContent(CurrentUserId ?? string.Empty), "ownerId");

        // Upload is a traditional route (not under /api/), requires session cookie or token in form
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{ServerUrl}/tickets/uploadattachment", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<Stream?> DownloadAttachmentAsync(string attachmentPath)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{ServerUrl}{attachmentPath}"));
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadAsStreamAsync();
        return null;
    }

    public string GetAttachmentUrl(string attachmentPath)
    {
        return $"{ServerUrl}{attachmentPath}";
    }

    public async Task<bool> DeleteAttachmentAsync(string ticketId, string attachmentId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync(
            $"{BaseUrl}/tickets/{ticketId}/attachments/remove/{attachmentId}"));
        return response.IsSuccessStatusCode;
    }

    // Reference data
    public async Task<string> GetStatusesAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets/status"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetUsersAsync()
    {
        var endpoint = IsV2 ? $"{BaseUrl}/accounts" : $"{BaseUrl}/users";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync(endpoint));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAssigneesAsync()
    {
        var endpoint = IsV2 ? $"{BaseUrl}/accounts?type=agents" : $"{BaseUrl}/users/getassignees";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync(endpoint));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketTypesAsync()
    {
        var endpoint = IsV2 ? $"{BaseUrl}/tickets/info/types" : $"{BaseUrl}/tickets/types";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync(endpoint));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetPrioritiesAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets/priorities"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTagsAsync()
    {
        // v1 has /tickets/tags (all tags) or /tags/limit (paginated)
        var endpoint = IsV2 ? $"{BaseUrl}/tags" : $"{V1BaseUrl}/tickets/tags";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync(endpoint));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetGroupsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/groups"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketsByGroupAsync(string groupId, int page = 0, int limit = 50)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync(
            $"{BaseUrl}/tickets/group/{groupId}?page={page}&limit={limit}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetOverdueTicketsAsync()
    {
        // overdue only exists in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/tickets/overdue"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> SubscribeToTicketAsync(string ticketId, bool subscribe)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrEmpty(CurrentUserId))
            return false;

        var payload = new { user = CurrentUserId, subscribe };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        // subscribe only exists in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V1BaseUrl}/tickets/{ticketId}/subscribe", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<string> GetNotificationsAsync()
    {
        // notifications only in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/users/notifications"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<int> GetNotificationCountAsync()
    {
        try
        {
            if (!_settings.IsConfigured || !IsAuthenticated)
                return 0;

            // notificationCount only in v1
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/users/notificationCount"));
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

    // Stats - these endpoints exist only in v1
    public async Task<string> GetTicketStatsAsync(int timespan = 30)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/tickets/stats/{timespan}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForGroupAsync(string groupId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/tickets/stats/group/{groupId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForUserAsync(string userId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/tickets/stats/user/{userId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // Recurring Tasks (v2 only)
    public async Task<string> GetRecurringTasksAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/recurring-tasks"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetRecurringTaskAsync(string taskId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/recurring-tasks/{taskId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateRecurringTaskAsync(Dictionary<string, object?> taskData)
    {
        var content = new StringContent(JsonSerializer.Serialize(taskData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{BaseUrl}/recurring-tasks", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateRecurringTaskAsync(string taskId, Dictionary<string, object?> taskData)
    {
        var content = new StringContent(JsonSerializer.Serialize(taskData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/recurring-tasks/{taskId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteRecurringTaskAsync(string taskId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{BaseUrl}/recurring-tasks/{taskId}"));
        return response.IsSuccessStatusCode;
    }

    // Assets (v2 only)
    public async Task<string> GetAssetsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/assets"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAssetAsync(string assetId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/assets/{assetId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateAssetAsync(Dictionary<string, object?> assetData)
    {
        var content = new StringContent(JsonSerializer.Serialize(assetData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{BaseUrl}/assets", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAssetAsync(string assetId, Dictionary<string, object?> assetData)
    {
        var content = new StringContent(JsonSerializer.Serialize(assetData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/assets/{assetId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAssetAsync(string assetId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{BaseUrl}/assets/{assetId}"));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LinkAssetToTicketAsync(string assetId, string ticketId)
    {
        var payload = new { ticketId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{BaseUrl}/assets/{assetId}/link-ticket", content));
        return response.IsSuccessStatusCode;
    }

    // Reports (v2)
    public async Task<string> GetHandoverReportAsync(string format = "json")
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/reports/handover?format={format}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetSitzungReportAsync(string format = "json")
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/reports/sitzung?format={format}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Sends an HTTP request and automatically retries with a refreshed token on 401.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAutoRefreshAsync(Func<Task<HttpResponseMessage>> requestFactory)
    {
        var response = await requestFactory();
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && IsV2 && !string.IsNullOrEmpty(_refreshToken))
        {
            if (await TryRefreshTokenAsync())
                response = await requestFactory();
        }
        return response;
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
