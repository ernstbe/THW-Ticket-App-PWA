using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using THWTicketApp.Shared.Models;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class TrueDeskApiService : ITrueDeskApiService
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly LocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    // Optional so existing unit-test constructions (4-arg) keep compiling; DI
    // always supplies it. Used to purge the offline cache on logout / foreign
    // login so a shared device doesn't leak the previous user's data.
    private readonly IIndexedDbService? _indexedDb;
    private string? _authToken;
    private string? _refreshToken;

    public string? CurrentUsername { get; private set; }
    public string? CurrentUserId { get; private set; }
    public string? LastError { get; private set; }

    public TrueDeskApiService(HttpClient httpClient, AppSettings settings, LocalStorageService localStorage, IJSRuntime jsRuntime, IIndexedDbService? indexedDb = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        _indexedDb = indexedDb;
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.ConnectionTimeoutSeconds);
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);

    private string BaseUrl => _settings.ApiBaseUrl.Trim().TrimEnd('/');
    private string ServerUrl => BaseUrl.Replace("/api/v2", "").Replace("/api/v1", "");
    private bool IsV2 => BaseUrl.Contains("/api/v2");
    // Some endpoints only exist in v1 - use this for those calls
    private string V1BaseUrl => ServerUrl + "/api/v1";
    // Newer CRUD endpoints (teams, departments, ticket-templates, calendar,
    // recurring tasks, assets) exist only under v2 regardless of the configured base URL.
    private string V2BaseUrl => ServerUrl + "/api/v2";

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
            // Per-install stable identifier. Trudesk's v1 login (PR #51)
            // uses this to slot the freshly minted token into a per-device
            // entry instead of stomping on other devices' sessions. Old
            // server versions ignore the extra field — safe to send always.
            var deviceId = await GetOrCreateDeviceIdAsync();

            var payload = new { username, password, deviceId };
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

                // v2: extract user info from JWT payload if not in response
                if (CurrentUserId == null && !string.IsNullOrEmpty(_authToken))
                {
                    try
                    {
                        var parts = _authToken.Split('.');
                        if (parts.Length == 3)
                        {
                            var payload64 = parts[1];
                            // Fix base64 padding
                            switch (payload64.Length % 4)
                            {
                                case 2: payload64 += "=="; break;
                                case 3: payload64 += "="; break;
                            }
                            var payloadBytes = Convert.FromBase64String(payload64);
                            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                            using var jwtDoc = JsonDocument.Parse(payloadJson);
                            if (jwtDoc.RootElement.TryGetProperty("user", out var jwtUser))
                            {
                                if (jwtUser.TryGetProperty("_id", out var jwtId))
                                    CurrentUserId = jwtId.GetString();
                                if (jwtUser.TryGetProperty("fullname", out var jwtName))
                                    username = jwtName.GetString() ?? username;
                            }
                        }
                    }
                    catch { /* JWT parsing optional */ }
                }

                CurrentUsername = username;

                // Foreign login on a shared device (including a passkey-locked
                // device where a different person logs in fresh instead of
                // unlocking): if the account differs from whatever identity was
                // cached here, purge the previous user's offline data before we
                // start caching this user's (#217).
                var previousUserId = await _localStorage.GetItemAsync("auth_userid");
                if (string.IsNullOrEmpty(previousUserId))
                    previousUserId = await _localStorage.GetItemAsync("locked_auth_userid");
                if (!string.IsNullOrEmpty(previousUserId)
                    && !string.Equals(previousUserId, CurrentUserId, StringComparison.Ordinal))
                {
                    await ClearOfflineCacheAsync();
                }

                await _localStorage.SetItemAsync("auth_token", _authToken ?? string.Empty);
                await _localStorage.SetItemAsync("auth_refresh_token", _refreshToken ?? string.Empty);
                await _localStorage.SetItemAsync("auth_username", username);
                await _localStorage.SetItemAsync("auth_userid", CurrentUserId ?? string.Empty);
                // Drop any stale locked-session keys from a previous logout-with-passkey.
                await ClearLockedAuthAsync();
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
            // Use SYNCHRONOUS JS calls via IJSInProcessRuntime (Blazor WASM only).
            // Async InvokeAsync and module imports both suffer from timing issues
            // during early init — GetAuthenticationStateAsync races with the JS bridge.
            // Synchronous calls bypass this entirely.
            if (_jsRuntime is Microsoft.JSInterop.IJSInProcessRuntime jsSync)
            {
                var storedToken = jsSync.Invoke<string?>("localStorage.getItem", "auth_token");
                if (!string.IsNullOrEmpty(storedToken))
                {
                    _authToken = storedToken;
                    _refreshToken = jsSync.Invoke<string?>("localStorage.getItem", "auth_refresh_token");
                    CurrentUsername = jsSync.Invoke<string?>("localStorage.getItem", "auth_username");
                    CurrentUserId = jsSync.Invoke<string?>("localStorage.getItem", "auth_userid");
                    SetAuthHeader(_authToken);
                    return true;
                }
            }
            else
            {
                // Fallback for non-WASM (shouldn't happen but just in case)
                var storedToken = await _localStorage.GetItemAsync("auth_token");
                if (!string.IsNullOrEmpty(storedToken))
                {
                    _authToken = storedToken;
                    _refreshToken = await _localStorage.GetItemAsync("auth_refresh_token");
                    CurrentUsername = await _localStorage.GetItemAsync("auth_username");
                    CurrentUserId = await _localStorage.GetItemAsync("auth_userid");
                    SetAuthHeader(_authToken);
                    return true;
                }
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

    public event Func<Task>? LoggingOut;

    public async Task LogoutAsync()
    {
        // Fire pre-logout hooks while we still have a valid token. Most
        // important consumer: WebPushService.DisableAsync, which has to
        // DELETE the subscription server-side BEFORE we drop our auth.
        // We deliberately run hooks for both the passkey-lock branch and
        // the plain logout branch — a user that "locks" their session
        // probably still doesn't want pushes flowing in.
        if (LoggingOut != null)
        {
            foreach (Func<Task> handler in LoggingOut.GetInvocationList().Cast<Func<Task>>())
            {
                try { await handler(); } catch { /* don't let a hook break logout */ }
            }
        }

        // If a passkey is registered on this device, treat logout as "lock":
        // keep the session token in a separate localStorage key that only the
        // explicit biometric-unlock flow reads. AuthStateProvider's auto-restore
        // looks at "auth_token" and finds nothing, so the user gets the login
        // screen — but the Passkey button can unlock without re-entering creds.
        var passkeyId = await _localStorage.GetItemAsync("passkey_credential_id");
        if (!string.IsNullOrEmpty(passkeyId) && IsAuthenticated)
        {
            await LockSessionAsync();
            return;
        }

        try
        {
            if (IsAuthenticated)
                await _httpClient.GetAsync($"{V1BaseUrl}/logout");
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
        _isAdminCached = null;
        SetAuthHeader(null);
        await _localStorage.RemoveItemAsync("auth_token");
        await _localStorage.RemoveItemAsync("auth_refresh_token");
        await _localStorage.RemoveItemAsync("auth_username");
        await _localStorage.RemoveItemAsync("auth_userid");
        await ClearLockedAuthAsync();
        // Full logout ends the session: purge the offline cache so the next user
        // on a shared device can't read the previous user's tickets, queued
        // offline actions or sync log (#217).
        await ClearOfflineCacheAsync();
    }

    // Best-effort wipe of the IndexedDB stores. Swallows failures so a cache
    // problem can never block logout/login itself.
    private async Task ClearOfflineCacheAsync()
    {
        if (_indexedDb == null) return;
        try { await _indexedDb.ClearTicketCacheAsync(); } catch { }
        try { await _indexedDb.ClearPendingActionsAsync(); } catch { }
        try { await _indexedDb.ClearSyncLogAsync(); } catch { }
    }

    private async Task ClearLockedAuthAsync()
    {
        await _localStorage.RemoveItemAsync("locked_auth_token");
        await _localStorage.RemoveItemAsync("locked_auth_refresh_token");
        await _localStorage.RemoveItemAsync("locked_auth_username");
        await _localStorage.RemoveItemAsync("locked_auth_userid");
    }

    /// <summary>
    /// Returns this install's stable device identifier, creating one on
    /// first use. Sent with every login so the server can rotate just
    /// THIS device's token slot instead of stomping on other devices.
    /// Persisted in localStorage under "device_id" — survives navigation
    /// and reloads but is cleared with the browser's site data, which is
    /// the right granularity (clearing site data ≈ "this is no longer
    /// the same install").
    /// </summary>
    internal async Task<string> GetOrCreateDeviceIdAsync()
    {
        var existing = await _localStorage.GetItemAsync("device_id");
        if (!string.IsNullOrWhiteSpace(existing)) return existing;

        var fresh = Guid.NewGuid().ToString("N");
        await _localStorage.SetItemAsync("device_id", fresh);
        return fresh;
    }

    private async Task LockSessionAsync()
    {
        // Move auth_* keys to locked_* so normal auto-restore won't pick them up.
        // Only TryUnlockSessionAsync (called after a successful biometric prompt)
        // moves them back. We intentionally do NOT call /logout on the server —
        // the v1 accessToken stays valid so unlock is instant.
        var token = _authToken ?? await _localStorage.GetItemAsync("auth_token");
        var refresh = _refreshToken ?? await _localStorage.GetItemAsync("auth_refresh_token");
        var username = CurrentUsername ?? await _localStorage.GetItemAsync("auth_username");
        var userid = CurrentUserId ?? await _localStorage.GetItemAsync("auth_userid");

        if (!string.IsNullOrEmpty(token))
            await _localStorage.SetItemAsync("locked_auth_token", token);
        if (!string.IsNullOrEmpty(refresh))
            await _localStorage.SetItemAsync("locked_auth_refresh_token", refresh);
        if (!string.IsNullOrEmpty(username))
            await _localStorage.SetItemAsync("locked_auth_username", username);
        if (!string.IsNullOrEmpty(userid))
            await _localStorage.SetItemAsync("locked_auth_userid", userid);

        _authToken = null;
        _refreshToken = null;
        CurrentUsername = null;
        CurrentUserId = null;
        // Drop the cached admin flag too — otherwise a different user who logs
        // in on this same (Scoped, whole-session) instance after the lock
        // inherits the locker's admin UI until a page reload (#208).
        _isAdminCached = null;
        SetAuthHeader(null);
        await _localStorage.RemoveItemAsync("auth_token");
        await _localStorage.RemoveItemAsync("auth_refresh_token");
        await _localStorage.RemoveItemAsync("auth_username");
        await _localStorage.RemoveItemAsync("auth_userid");
    }

    public async Task<bool> TryUnlockSessionAsync()
    {
        // Called by Login.razor after navigator.credentials.get() succeeded.
        // Moves locked_* keys back to auth_* and then performs the normal restore.
        var token = await _localStorage.GetItemAsync("locked_auth_token");
        if (string.IsNullOrEmpty(token))
        {
            // No locked session — fall through to a normal restore in case
            // auth_* still exists (e.g. first-ever unlock after registering passkey).
            return await TryRestoreSessionAsync();
        }

        var refresh = await _localStorage.GetItemAsync("locked_auth_refresh_token");
        var username = await _localStorage.GetItemAsync("locked_auth_username");
        var userid = await _localStorage.GetItemAsync("locked_auth_userid");

        await _localStorage.SetItemAsync("auth_token", token);
        if (!string.IsNullOrEmpty(refresh))
            await _localStorage.SetItemAsync("auth_refresh_token", refresh);
        if (!string.IsNullOrEmpty(username))
            await _localStorage.SetItemAsync("auth_username", username);
        if (!string.IsNullOrEmpty(userid))
            await _localStorage.SetItemAsync("auth_userid", userid);

        await ClearLockedAuthAsync();
        return await TryRestoreSessionAsync();
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

    public async Task<(int StatusCode, string Body)> GetTicketRawAsync(string ticketUid)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{BaseUrl}/tickets/{ticketUid}"));
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, body);
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

    public async Task<TicketCreateResult?> CreateTicketAsync(string subject, string? issue, string? typeId, string? priorityId, string? groupId, string? assigneeId, DateTime? dueDate = null, IReadOnlyList<string>? checklist = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;

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
        if (dueDate.HasValue) payload["dueDate"] = dueDate.Value.ToString("O");
        // Template checklist rides along in the create payload — the server
        // (trudesk PR #106) validates `checklist` on the ticketsV2 create
        // and stores the items with completed:false.
        if (checklist is { Count: > 0 })
            payload["checklist"] = checklist
                .Select(t => new Dictionary<string, object?> { ["title"] = t })
                .ToList();

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        // v1 uses /tickets/create, v2 uses /tickets
        var endpoint = IsV2 ? $"{BaseUrl}/tickets" : $"{BaseUrl}/tickets/create";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync(endpoint, content));
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            LastError = $"{(int)response.StatusCode}: {responseBody}";
            return null;
        }
        // Both v1 (`{ success, ticket: { _id, uid, ... } }`) and v2 wrap the
        // created ticket under `ticket`. Surface id + uid so callers can chain
        // follow-ups (attachments via _id, checklist items via uid). On parse
        // trouble we still treat the create as a success and return empty
        // id / zero uid — callers that need them check explicitly.
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("ticket", out var ticket))
            {
                var id = ticket.TryGetProperty("_id", out var idEl)
                    ? idEl.GetString() ?? string.Empty
                    : string.Empty;
                var uid = ticket.TryGetProperty("uid", out var uidEl) &&
                          uidEl.ValueKind == JsonValueKind.Number &&
                          uidEl.TryGetInt32(out var parsedUid)
                    ? parsedUid
                    : 0;
                return new TicketCreateResult(id, uid);
            }
        }
        catch { /* malformed body — fall through to empty result */ }
        return new TicketCreateResult(string.Empty, 0);
    }

    public async Task<bool> EditTicketAsync(Ticket ticket, bool includeDueDate = true)
    {
        if (ticket == null || string.IsNullOrWhiteSpace(ticket.Id))
            return false;

        var ticketData = new Dictionary<string, object?>();
        if (ticket.Subject != null) ticketData["subject"] = ticket.Subject;
        if (ticket.Issue != null) ticketData["issue"] = ticket.Issue;
        if (ticket.Priority?.Id != null) ticketData["priority"] = ticket.Priority.Id;
        if (ticket.Status?.Id != null) ticketData["status"] = ticket.Status.Id;
        if (ticket.Type?.Id != null) ticketData["type"] = ticket.Type.Id;
        if (ticket.Group?.Id != null) ticketData["group"] = ticket.Group.Id;
        // By default always send dueDate: MinValue means "no due date" and
        // must go out as an explicit null, otherwise clearing the date never
        // reaches the server (trudesk treats a missing key as "leave
        // unchanged"). Partial updates replayed from the offline sync queue
        // pass includeDueDate=false so an unrelated edit (e.g. subject-only)
        // doesn't clobber the server-side due date.
        if (includeDueDate)
            ticketData["dueDate"] = ticket.DueDate == DateTime.MinValue ? null : ticket.DueDate.ToString("O");

        // v2 expects { ticket: {...} } wrapper, v1 expects flat object
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        // v2 uses uid, v1 uses _id
        var identifier = IsV2 ? ticket.Uid.ToString() : ticket.Id;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{identifier}", content));
        return response.IsSuccessStatusCode;
    }

    // ticketUid is required for v2: its ticket routes match on :uid, while v1
    // matches on the Mongo _id. We branch the URL path the same way
    // EditTicketAsync does (IsV2 ? uid : _id) so the live v1 behaviour stays
    // byte-for-byte identical.
    public async Task<bool> DeleteTicketAsync(string ticketId, int ticketUid)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return false;
        var identifier = IsV2 ? ticketUid.ToString() : ticketId;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{BaseUrl}/tickets/{identifier}"));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateTicketStatusAsync(string ticketId, int ticketUid, string statusId)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(statusId))
            return false;

        var ticketData = new Dictionary<string, object?> { ["status"] = statusId };
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var identifier = IsV2 ? ticketUid.ToString() : ticketId;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{identifier}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AssignTicketAsync(string ticketId, int ticketUid, string userId)
    {
        var ticketData = new Dictionary<string, object?> { ["assignee"] = userId };
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var identifier = IsV2 ? ticketUid.ToString() : ticketId;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{identifier}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearTicketAssigneeAsync(string ticketId, int ticketUid)
    {
        var ticketData = new Dictionary<string, object?> { ["assignee"] = null };
        object payload = IsV2 ? new { ticket = ticketData } : ticketData;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var identifier = IsV2 ? ticketUid.ToString() : ticketId;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{identifier}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetAdditionalAssigneesAsync(string ticketId, IEnumerable<string> userIds)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return false;
        // PUT /tickets/:id/additional-assignees only exists in v1
        // (trudesk-thw feat/additional-assignees). Replaces the whole array;
        // empty array clears. The server de-duplicates and drops the primary
        // assignee id.
        var payload = new { additionalAssignees = userIds.ToArray() };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync(
            $"{V1BaseUrl}/tickets/{ticketId}/additional-assignees", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddCommentAsync(string ticketUid, string ownerId, string newComment)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || string.IsNullOrWhiteSpace(newComment))
            return false;

        var payload = new { comment = newComment };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/tickets/{ticketUid}/comments", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddNoteAsync(string ticketUid, string ownerId, string note)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || string.IsNullOrWhiteSpace(note))
            return false;

        var payload = new { note };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/tickets/{ticketUid}/notes", content));
        return response.IsSuccessStatusCode;
    }

    // Attachments
    public async Task<bool> UploadAttachmentAsync(string ticketId, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
        content.Add(streamContent, "file", fileName);

        // Token-authenticated endpoint added in trudesk-thw PR #96.
        // Ticket id is in the URL, ownerId is taken from req.user server-side —
        // we don't pass them as form fields (would be ignored anyway).
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync(
            $"{V1BaseUrl}/tickets/{ticketId}/attachments", content));
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

    public string? GetUserAvatarUrl(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return null;
        // trudesk hands out "defaultProfile.jpg" for users who never uploaded a
        // picture — treat that (and any default* placeholder) as "no avatar" so
        // the UserAvatar component renders coloured initials instead.
        if (image.StartsWith("defaultProfile", StringComparison.OrdinalIgnoreCase)) return null;
        return $"{ServerUrl}/uploads/users/{image}";
    }

    public async Task<bool> DeleteAttachmentAsync(string ticketId, string attachmentId)
    {
        // /tickets/:tid/attachments/remove/:aid only exists in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync(
            $"{V1BaseUrl}/tickets/{ticketId}/attachments/remove/{attachmentId}"));
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

    public async Task<string> GetTagsAsync()
    {
        // Tags only exist in v1 — there is no v2 /tags endpoint
        var endpoint = $"{V1BaseUrl}/tickets/tags";
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
        // /tickets/group/:id only exists in v1
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync(
            $"{V1BaseUrl}/tickets/group/{groupId}?page={page}&limit={limit}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetOverdueTicketsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/tickets/overdue"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> SubscribeToTicketAsync(string ticketUid, bool subscribe)
    {
        if (string.IsNullOrWhiteSpace(ticketUid))
            return false;

        var payload = new { subscribe };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/tickets/{ticketUid}/subscribe", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<string> GetNotificationsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/users/notifications"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<int> GetNotificationCountAsync()
    {
        try
        {
            if (!_settings.IsConfigured || !IsAuthenticated)
                return 0;
            if (!CanCallV2) return 0;

            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/users/notifications/count"));
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

    public async Task<bool> MarkNotificationReadAsync(string notificationId)
    {
        if (string.IsNullOrEmpty(notificationId)) return false;
        try
        {
            // v1 endpoint — server-side patch landed in trudesk PR #47.
            var response = await SendWithAutoRefreshAsync(() =>
                _httpClient.PostAsync($"{V1BaseUrl}/users/notifications/{notificationId}/markRead", null));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<int> MarkAllNotificationsReadAsync()
    {
        try
        {
            var response = await SendWithAutoRefreshAsync(() =>
                _httpClient.PostAsync($"{V1BaseUrl}/users/notifications/markAllRead", null));
            if (!response.IsSuccessStatusCode) return 0;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("updated", out var u) && u.ValueKind == JsonValueKind.Number
                ? u.GetInt32()
                : 0;
        }
        catch { return 0; }
    }

    public async Task<string> GetTicketStatsAsync(int timespan = 30)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/tickets/stats/{timespan}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForGroupAsync(string groupId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/tickets/stats/group/{groupId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForUserAsync(string userId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/tickets/stats/user/{userId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketStatsForAssigneeAsync(string userId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/tickets/stats/assignee/{userId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // Per-assignee workload, scoped server-side to the caller's visible
    // groups — only assignees on tickets the user may see are returned.
    public async Task<string> GetWorkloadStatsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/tickets/stats/workload"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // Documents (v2)
    public async Task<string> GetDocumentsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/documents"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateDocumentAsync(string name, string? description, string? category)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var payload = new Dictionary<string, object?> { ["name"] = name };
        if (!string.IsNullOrEmpty(description)) payload["description"] = description;
        if (!string.IsNullOrEmpty(category)) payload["category"] = category;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/documents", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return false;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/documents/{documentId}"));
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------
    // Notices (v2)
    // -----------------------------------------------------------------

    public async Task<string> GetNoticesAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/notices"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateNoticeAsync(string name, string message, string color, string fontColor)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(message)) return false;
        var payload = new { name, message, color, fontColor };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/notices", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ActivateNoticeAsync(string noticeId)
    {
        if (string.IsNullOrWhiteSpace(noticeId)) return false;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/notices/{noticeId}/activate",
            new StringContent("{}", Encoding.UTF8, "application/json")));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearNoticesAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/notices/clear"));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteNoticeAsync(string noticeId)
    {
        if (string.IsNullOrWhiteSpace(noticeId)) return false;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/notices/{noticeId}"));
        return response.IsSuccessStatusCode;
    }

    // Dashboard (v2)
    public async Task<string> GetDashboardWidgetsAsync()
    {
        if (!CanCallV2) return "{}";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/dashboard/widgets"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // Recurring Tasks (v2 only)
    public async Task<string> GetRecurringTasksAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/recurring-tasks"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetRecurringTaskAsync(string taskId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/recurring-tasks/{taskId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateRecurringTaskAsync(Dictionary<string, object?> taskData)
    {
        var content = new StringContent(JsonSerializer.Serialize(taskData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/recurring-tasks", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateRecurringTaskAsync(string taskId, Dictionary<string, object?> taskData)
    {
        var content = new StringContent(JsonSerializer.Serialize(taskData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/recurring-tasks/{taskId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteRecurringTaskAsync(string taskId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/recurring-tasks/{taskId}"));
        return response.IsSuccessStatusCode;
    }

    // Assets (v2 only)
    public async Task<string> GetAssetsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/assets"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAssetAsync(string assetId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/assets/{assetId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateAssetAsync(Dictionary<string, object?> assetData)
    {
        var content = new StringContent(JsonSerializer.Serialize(assetData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/assets", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAssetAsync(string assetId, Dictionary<string, object?> assetData)
    {
        var content = new StringContent(JsonSerializer.Serialize(assetData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/assets/{assetId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAssetAsync(string assetId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/assets/{assetId}"));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LinkAssetToTicketAsync(string assetId, string ticketUid)
    {
        if (string.IsNullOrWhiteSpace(assetId) || string.IsNullOrWhiteSpace(ticketUid))
            return false;
        // The backend expects { ticketUid: "<uid>" }, not { ticketId }.
        var payload = new { ticketUid };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/assets/{assetId}/link-ticket", content));
        return response.IsSuccessStatusCode;
    }

    // Reports (v2)
    public async Task<string> GetHandoverReportAsync(string format = "json")
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/reports/handover?format={format}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetSitzungReportAsync(string format = "json")
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/reports/sitzung?format={format}"));
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

    /// <summary>
    /// Checks whether v2 endpoints can be called. trudesk's apiv2 middleware
    /// accepts both the v2 JWT bearer header AND the v1 accesstoken header as
    /// a fallback, so any authenticated client (v1 or v2 mode) can hit v2
    /// endpoints — we just need to have some token attached.
    /// </summary>
    private bool CanCallV2 => IsAuthenticated;

    /// <summary>
    /// Guard for v2-only endpoints. Throws HttpRequestException so callers'
    /// existing try/catch handles it like any other failure.
    /// </summary>
    private static void ThrowV2Unavailable() =>
        throw new HttpRequestException("v2 endpoint unavailable in v1 auth mode");

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

    // -----------------------------------------------------------------
    // Teams (v2)
    // -----------------------------------------------------------------

    public async Task<string> GetTeamsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/teams"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTeamAsync(string teamId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/teams/{teamId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateTeamAsync(Dictionary<string, object?> teamData)
    {
        var content = new StringContent(JsonSerializer.Serialize(teamData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/teams", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateTeamAsync(string teamId, Dictionary<string, object?> teamData)
    {
        var content = new StringContent(JsonSerializer.Serialize(teamData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/teams/{teamId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteTeamAsync(string teamId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/teams/{teamId}"));
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------
    // Departments (v2)
    // -----------------------------------------------------------------

    public async Task<string> GetDepartmentsAsync()
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/departments"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetDepartmentAsync(string departmentId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/departments/{departmentId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateDepartmentAsync(Dictionary<string, object?> departmentData)
    {
        var content = new StringContent(JsonSerializer.Serialize(departmentData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/departments", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateDepartmentAsync(string departmentId, Dictionary<string, object?> departmentData)
    {
        var content = new StringContent(JsonSerializer.Serialize(departmentData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/departments/{departmentId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteDepartmentAsync(string departmentId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/departments/{departmentId}"));
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------
    // Ticket Templates (v2)
    // -----------------------------------------------------------------

    public async Task<string> GetTicketTemplatesAsync()
    {
        if (!CanCallV2) return "[]";
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/ticket-templates"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetTicketTemplateAsync(string templateId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V2BaseUrl}/ticket-templates/{templateId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CreateTicketTemplateAsync(Dictionary<string, object?> templateData)
    {
        var content = new StringContent(JsonSerializer.Serialize(templateData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/ticket-templates", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateTicketTemplateAsync(string templateId, Dictionary<string, object?> templateData)
    {
        var content = new StringContent(JsonSerializer.Serialize(templateData), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/ticket-templates/{templateId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteTicketTemplateAsync(string templateId)
    {
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/ticket-templates/{templateId}"));
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------
    // Calendar (v2)
    // -----------------------------------------------------------------

    public async Task<string> GetCalendarEventsAsync(DateTime start, DateTime end)
    {
        // Backend contract: start and end are both REQUIRED query params.
        // trudesk/src/controllers/api/v2/calendar.js:8-10 rejects missing bounds
        // with a 400, and reads them as `start` / `end` — not `from` / `to`.
        var startIso = Uri.EscapeDataString(start.ToUniversalTime().ToString("O"));
        var endIso = Uri.EscapeDataString(end.ToUniversalTime().ToString("O"));
        var response = await SendWithAutoRefreshAsync(() =>
            _httpClient.GetAsync($"{V2BaseUrl}/calendar/events?start={startIso}&end={endIso}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // -----------------------------------------------------------------
    // Ticket tags — implemented on top of the existing ticket update endpoint.
    // trudesk doesn't expose a dedicated /tickets/:uid/tags endpoint, so Add/Remove
    // do a read-modify-write cycle via GetTicketRawAsync + PUT /tickets/:uid.
    // -----------------------------------------------------------------

    // Tag mutations target the same ticket update endpoint, so they need the
    // same _id (v1) vs uid (v2) branching. The read-modify-write helpers also
    // GET the ticket first — and v2's GET matches on uid too — so the uid is
    // threaded all the way through.
    public async Task<bool> UpdateTicketTagsAsync(string ticketId, int ticketUid, IEnumerable<string> tagIds)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return false;
        var payload = new Dictionary<string, object?> { ["tags"] = tagIds.ToArray() };
        object body = IsV2 ? new { ticket = payload } : (object)payload;
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var identifier = IsV2 ? ticketUid.ToString() : ticketId;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{BaseUrl}/tickets/{identifier}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddTagToTicketAsync(string ticketId, int ticketUid, string tagId)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(tagId)) return false;
        var current = await ReadTicketTagsAsync(ticketId, ticketUid);
        if (current == null) return false;
        if (current.Contains(tagId)) return true; // already present, no-op
        current.Add(tagId);
        return await UpdateTicketTagsAsync(ticketId, ticketUid, current);
    }

    public async Task<bool> RemoveTagFromTicketAsync(string ticketId, int ticketUid, string tagId)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(tagId)) return false;
        var current = await ReadTicketTagsAsync(ticketId, ticketUid);
        if (current == null) return false;
        if (!current.Remove(tagId)) return true; // already absent, no-op
        return await UpdateTicketTagsAsync(ticketId, ticketUid, current);
    }

    private async Task<List<string>?> ReadTicketTagsAsync(string ticketId, int ticketUid)
    {
        var (status, body) = await GetTicketRawAsync(IsV2 ? ticketUid.ToString() : ticketId);
        if (status < 200 || status >= 300) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            JsonElement ticketEl;
            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                ticketEl = dataEl;
            else if (doc.RootElement.TryGetProperty("ticket", out var tEl))
                ticketEl = tEl;
            else
                ticketEl = doc.RootElement;

            if (!ticketEl.TryGetProperty("tags", out var tagsEl) || tagsEl.ValueKind != JsonValueKind.Array)
                return new List<string>();

            var result = new List<string>();
            foreach (var t in tagsEl.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String)
                {
                    var s = t.GetString();
                    if (!string.IsNullOrEmpty(s)) result.Add(s);
                }
                else if (t.ValueKind == JsonValueKind.Object)
                {
                    if (t.TryGetProperty("_id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                        result.Add(idEl.GetString()!);
                    else if (t.TryGetProperty("id", out var idEl2) && idEl2.ValueKind == JsonValueKind.String)
                        result.Add(idEl2.GetString()!);
                }
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    // -----------------------------------------------------------------
    // Ticket Checklist (v2)
    // -----------------------------------------------------------------

    public async Task<bool> AddChecklistItemAsync(string ticketUid, string title)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || string.IsNullOrWhiteSpace(title))
            return false;
        var payload = new { title };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/tickets/{ticketUid}/checklist", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateChecklistItemAsync(string ticketUid, string itemId, string? title = null, bool? completed = null)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || string.IsNullOrWhiteSpace(itemId))
            return false;
        var payload = new Dictionary<string, object?>();
        if (title != null) payload["title"] = title;
        if (completed != null) payload["completed"] = completed;
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/tickets/{ticketUid}/checklist/{itemId}", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteChecklistItemAsync(string ticketUid, string itemId)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || string.IsNullOrWhiteSpace(itemId))
            return false;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/tickets/{ticketUid}/checklist/{itemId}"));
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------
    // Linked tickets (v2, bidirectional)
    // -----------------------------------------------------------------

    public async Task<bool> LinkTicketAsync(string ticketUid, int targetUid, string linkType)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || targetUid <= 0)
            return false;
        var payload = new { targetUid, linkType };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/tickets/{ticketUid}/links", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnlinkTicketAsync(string ticketUid, int targetUid)
    {
        if (string.IsNullOrWhiteSpace(ticketUid) || targetUid <= 0)
            return false;
        var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V2BaseUrl}/tickets/{ticketUid}/links/{targetUid}"));
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------
    // Batch operations (v2)
    // -----------------------------------------------------------------

    public async Task<(int Deleted, int Failed)> BatchDeleteTicketsAsync(IEnumerable<string> ticketIds)
    {
        var ids = ticketIds.ToArray();
        if (ids.Length == 0) return (0, 0);
        var payloadJson = JsonSerializer.Serialize(new { ids });
        // Build the HttpRequestMessage (and its content) INSIDE the factory:
        // SendWithAutoRefreshAsync re-invokes it on a 401+refresh, and an
        // HttpRequestMessage/StringContent can only be sent once — reusing a
        // single instance throws InvalidOperationException on the retry
        // (see UnsubscribeWebPushAsync for the same pattern).
        var response = await SendWithAutoRefreshAsync(() => _httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"{V2BaseUrl}/tickets/batch")
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            }));
        if (!response.IsSuccessStatusCode) return (0, ids.Length);
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var deleted = doc.RootElement.TryGetProperty("deleted", out var d) ? d.GetInt32() : 0;
            var failed = doc.RootElement.TryGetProperty("failed", out var f) ? f.GetInt32() : 0;
            return (deleted, failed);
        }
        catch { return (ids.Length, 0); }
    }

    public async Task<(int Updated, int Failed)> BatchUpdateTicketsAsync(IEnumerable<Dictionary<string, object?>> batch)
    {
        var batchArray = batch.ToArray();
        if (batchArray.Length == 0) return (0, 0);
        var payload = new { batch = batchArray };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/tickets/batch", content));
        if (!response.IsSuccessStatusCode) return (0, batchArray.Length);
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            // Server returns { success: true, updated: <n>, failed: <n> }.
            // (Older builds put the count in `success`; fall back for safety.)
            var updated = doc.RootElement.TryGetProperty("updated", out var u) ? u.GetInt32()
                : (doc.RootElement.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0);
            var failed = doc.RootElement.TryGetProperty("failed", out var f) ? f.GetInt32() : 0;
            return (updated, failed);
        }
        catch { return (batchArray.Length, 0); }
    }

    // -----------------------------------------------------------------
    // Profile (v2)
    // -----------------------------------------------------------------

    public async Task<UserProfile?> GetCurrentUserProfileAsync()
    {
        try
        {
            // GET /api/v2/login returns the full session user document.
            // The trudesk handler is accountsApi.sessionUser; PR #41's
            // v1-token fallback in apiv2 middleware means our accesstoken
            // header works here too.
            var response = await SendWithAutoRefreshAsync(() =>
                _httpClient.GetAsync($"{V2BaseUrl}/login"));
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(json);

            // sessionUser returns the raw user object; some other endpoints
            // wrap responses. Handle both.
            var root = doc.RootElement;
            JsonElement userEl;
            if (root.TryGetProperty("user", out var u)) userEl = u;
            else if (root.TryGetProperty("account", out var a)) userEl = a;
            else userEl = root;

            // Group ids resolved server-side: admins/agents get the
            // Department-chain-derived list; customers get direct
            // Group.members. Either way the resulting count is what the
            // Kanban group-filter UI should base its visibility on.
            var groupIds = new List<string>();
            if (userEl.TryGetProperty("groups", out var grEl) && grEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in grEl.EnumerateArray())
                {
                    if (g.ValueKind == JsonValueKind.String)
                    {
                        var s = g.GetString();
                        if (!string.IsNullOrEmpty(s)) groupIds.Add(s);
                    }
                    else if (g.ValueKind == JsonValueKind.Object && g.TryGetProperty("_id", out var gIdEl))
                    {
                        var s = gIdEl.GetString();
                        if (!string.IsNullOrEmpty(s)) groupIds.Add(s);
                    }
                }
            }

            return new UserProfile
            {
                Id = userEl.TryGetProperty("_id", out var idEl) ? idEl.GetString() : null,
                Username = userEl.TryGetProperty("username", out var unEl) ? unEl.GetString() : null,
                Fullname = userEl.TryGetProperty("fullname", out var fnEl) ? fnEl.GetString() : null,
                Email = userEl.TryGetProperty("email", out var emEl) ? emEl.GetString() : null,
                Title = userEl.TryGetProperty("title", out var tEl) ? tEl.GetString() : null,
                WorkNumber = userEl.TryGetProperty("workNumber", out var wnEl) ? wnEl.GetString() : null,
                MobileNumber = userEl.TryGetProperty("mobileNumber", out var mnEl) ? mnEl.GetString() : null,
                Image = userEl.TryGetProperty("image", out var imgEl) ? imgEl.GetString() : null,
                Groups = groupIds,
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> ExportMyDataAsync()
    {
        // v2-only endpoint (trudesk PR feat/dsgvo-account-export). The
        // response body is the export document itself — return it verbatim
        // so the page can hand it to the download helper unchanged.
        var response = await SendWithAutoRefreshAsync(() =>
            _httpClient.GetAsync($"{V2BaseUrl}/accounts/me/export"));
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> UpdateProfileAsync(string fullname, string? title, string? workNumber, string? mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(fullname)) return false;
        var payload = new Dictionary<string, object?>
        {
            ["_id"] = CurrentUserId,
            ["username"] = CurrentUsername,
            ["fullname"] = fullname,
            ["title"] = title ?? string.Empty,
            ["workNumber"] = workNumber ?? string.Empty,
            ["mobileNumber"] = mobileNumber ?? string.Empty
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PutAsync($"{V2BaseUrl}/accounts/profile", content));
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> UploadProfileImageAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
        content.Add(streamContent, "file", fileName);

        // v2-only (trudesk feat/account-profile-image). The server targets
        // req.user._id, so we pass no user id. Returns the new image filename.
        var response = await SendWithAutoRefreshAsync(() =>
            _httpClient.PostAsync($"{V2BaseUrl}/accounts/profile/picture", content));
        if (!response.IsSuccessStatusCode) return null;
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("image", out var img) ? img.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Absolute URL of a user avatar served by trudesk under
    /// <c>/uploads/users/</c>, or null when the filename is empty.
    /// </summary>
    public string? BuildUserImageUrl(string? image)
        => string.IsNullOrWhiteSpace(image) ? null : $"{ServerUrl}/uploads/users/{image}";

    // ── Active sessions (multi-device tokens — trudesk PR #52) ───────────

    public async Task<List<SessionInfo>> GetSessionsAsync()
    {
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/account/sessions"));
            if (!response.IsSuccessStatusCode) return new List<SessionInfo>();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("sessions", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new List<SessionInfo>();

            var list = new List<SessionInfo>();
            foreach (var item in arr.EnumerateArray())
            {
                list.Add(new SessionInfo
                {
                    DeviceId = item.TryGetProperty("deviceId", out var dEl) && dEl.ValueKind == JsonValueKind.String ? dEl.GetString() : null,
                    UserAgent = item.TryGetProperty("userAgent", out var uEl) && uEl.ValueKind == JsonValueKind.String ? uEl.GetString() : null,
                    CreatedAt = item.TryGetProperty("createdAt", out var cEl) && cEl.ValueKind == JsonValueKind.String ? cEl.GetDateTime() : null,
                    LastUsedAt = item.TryGetProperty("lastUsedAt", out var lEl) && lEl.ValueKind == JsonValueKind.String ? lEl.GetDateTime() : null,
                    IsCurrent = item.TryGetProperty("isCurrent", out var icEl) && icEl.ValueKind == JsonValueKind.True,
                    IsLegacy = item.TryGetProperty("isLegacy", out var ilEl) && ilEl.ValueKind == JsonValueKind.True
                });
            }
            return list;
        }
        catch
        {
            return new List<SessionInfo>();
        }
    }

    public async Task<bool> RevokeSessionAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return false;
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V1BaseUrl}/account/sessions/{Uri.EscapeDataString(deviceId)}"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> RevokeAllOtherSessionsAsync()
    {
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V1BaseUrl}/account/sessions"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Web Push (v1) ───────────────────────────────────────────────────
    public async Task<string?> GetWebPushVapidPublicKeyAsync()
    {
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/account/push/vapid-public"));
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("publicKey", out var k) ? k.GetString() : null;
        }
        catch { return null; }
    }

    public async Task<bool> SubscribeWebPushAsync(string endpoint, string p256dh, string auth, string? deviceId, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
            return false;
        var payload = new
        {
            endpoint,
            keys = new { p256dh, auth },
            deviceId,
            userAgent
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V1BaseUrl}/account/push/subscribe", content));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> UnsubscribeWebPushAsync(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return false;
        var payload = new { endpoint };
        try
        {
            var response = await SendWithAutoRefreshAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Delete, $"{V1BaseUrl}/account/push/subscribe")
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                return _httpClient.SendAsync(req);
            });
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Bug reports (v1) ────────────────────────────────────────────────
    public async Task<bool> SubmitBugReportAsync(string title, string? description, Dictionary<string, object?>? context)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var payload = new { title, description, context };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V1BaseUrl}/bug-reports", content));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<BugReport>> ListBugReportsAsync()
    {
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/bug-reports"));
            if (!response.IsSuccessStatusCode) return new();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("reports", out var arr)) return new();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<BugReport>>(arr.GetRawText(), options) ?? new();
            return list;
        }
        catch { return new(); }
    }

    public async Task<bool> SetBugReportResolvedAsync(string id, bool resolved)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var payload = new { resolved };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            var response = await SendWithAutoRefreshAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Patch, $"{V1BaseUrl}/bug-reports/{id}") { Content = content };
                return _httpClient.SendAsync(req);
            });
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteBugReportAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.DeleteAsync($"{V1BaseUrl}/bug-reports/{id}"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private bool? _isAdminCached;

    public async Task<bool> IsCurrentUserAdminAsync()
    {
        if (_isAdminCached.HasValue) return _isAdminCached.Value;
        if (!IsAuthenticated) return false;
        try
        {
            var response = await SendWithAutoRefreshAsync(() => _httpClient.GetAsync($"{V1BaseUrl}/login"));
            if (!response.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("user", out var user)) return false;
            if (!user.TryGetProperty("role", out var role)) return false;
            string? normalized = null;
            if (role.ValueKind == JsonValueKind.Object && role.TryGetProperty("normalized", out var n))
                normalized = n.GetString();
            _isAdminCached = normalized == "admin";
            return _isAdminCached.Value;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdatePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return false;
        var payload = new { currentPassword, newPassword, confirmPassword };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await SendWithAutoRefreshAsync(() => _httpClient.PostAsync($"{V2BaseUrl}/accounts/profile/update-password", content));
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            LastError = body;
        }
        return response.IsSuccessStatusCode;
    }

    // ── Public registration (v1, no auth required) ──────────────────────

    public async Task<string?> GetCaptchaSvgAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ServerUrl}/captcha");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();
            return null;
        }
        catch { return null; }
    }

    public async Task<(bool Success, bool Exists, string? Error)> CheckEmailAsync(string email, string captcha)
    {
        try
        {
            var payload = new { email, captcha };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{V1BaseUrl}/public/users/checkemail", content);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var success = doc.RootElement.TryGetProperty("success", out var sEl) && sEl.GetBoolean();
            var exists = doc.RootElement.TryGetProperty("exist", out var eEl) && eEl.GetBoolean();
            var error = doc.RootElement.TryGetProperty("error", out var errEl) ? errEl.GetString() : null;

            return (success, exists, error);
        }
        catch (Exception ex)
        {
            return (false, false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string username, string fullname, string email, string password, string captcha)
    {
        try
        {
            var payload = new
            {
                user = new { username, fullname, email, password },
                captcha
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{V1BaseUrl}/public/account/create", content);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var success = doc.RootElement.TryGetProperty("success", out var sEl) && sEl.GetBoolean();
            var error = doc.RootElement.TryGetProperty("error", out var errEl) ? errEl.GetString() : null;

            return (success, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
