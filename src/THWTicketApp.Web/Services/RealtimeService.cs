using Microsoft.JSInterop;
using THWTicketApp.Shared.Services;

namespace THWTicketApp.Web.Services;

public class RealtimeService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly AppSettings _settings;
    private readonly LocalStorageService _localStorage;
    private IJSObjectReference? _module;
    private DotNetObjectReference<RealtimeService>? _dotNetRef;

    // Legacy generic event kept for backward compatibility with BrowserNotificationService.
    public event Action<string, string>? TicketEvent;

    // Strongly-typed per-channel events. Payload: Mongo ticket _id (empty for NotificationUpdate).
    public event Action<string>? TicketCreated;
    public event Action<string>? TicketUpdated;
    public event Action<string>? StatusUpdated;
    public event Action<string>? AssigneeUpdated;
    public event Action<string>? PriorityUpdated;
    public event Action<string>? TypeUpdated;
    public event Action<string>? GroupUpdated;
    public event Action<string>? TagsUpdated;
    public event Action<string>? DuedateUpdated;
    public event Action<string>? AttachmentsUpdated;
    public event Action<string>? CommentNoteChanged;
    public event Action? NotificationUpdate;

    // Convenience fan-in for list-style pages that want to refresh on any ticket change.
    public event Action<string>? AnyTicketChanged;

    public event Action<bool>? ConnectionStateChanged;
    public bool IsConnected { get; private set; }

    public RealtimeService(IJSRuntime jsRuntime, AppSettings settings, LocalStorageService localStorage)
    {
        _jsRuntime = jsRuntime;
        _settings = settings;
        _localStorage = localStorage;
    }

    public async Task ConnectAsync()
    {
        try
        {
            if (!_settings.IsConfigured) return;

            var token = await _localStorage.GetItemAsync("auth_token");
            if (string.IsNullOrEmpty(token)) return;

            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/realtime-interop.js");

            _dotNetRef = DotNetObjectReference.Create(this);
            var serverUrl = _settings.ApiBaseUrl.Replace("/api/v1", "").Replace("/api/v2", "");
            await _module.InvokeAsync<bool>("connect", serverUrl, token, _dotNetRef);
        }
        catch
        {
            // Socket connection is best-effort
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_module != null)
                await _module.InvokeVoidAsync("disconnect");
        }
        catch { }
        IsConnected = false;
    }

    [JSInvokable]
    public void OnConnected()
    {
        IsConnected = true;
        ConnectionStateChanged?.Invoke(true);
    }

    [JSInvokable]
    public void OnDisconnected()
    {
        IsConnected = false;
        ConnectionStateChanged?.Invoke(false);
    }

    [JSInvokable]
    public void OnTicketEvent(string eventName, string ticketId)
    {
        ticketId ??= string.Empty;

        switch (eventName)
        {
            case "ticketCreated":
                TicketCreated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "ticketUpdated":
                TicketUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "statusUpdated":
                StatusUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "assigneeUpdated":
                AssigneeUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "priorityUpdated":
                PriorityUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "typeUpdated":
                TypeUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "groupUpdated":
                GroupUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "tagsUpdated":
                TagsUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "duedateUpdated":
                DuedateUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "attachmentsUpdated":
                AttachmentsUpdated?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "commentNoteAdded":
            case "commentNoteRemoved":
                CommentNoteChanged?.Invoke(ticketId);
                AnyTicketChanged?.Invoke(ticketId);
                break;
            case "notificationUpdate":
                NotificationUpdate?.Invoke();
                break;
        }

        TicketEvent?.Invoke(eventName, ticketId);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _dotNetRef?.Dispose();
        if (_module != null)
            await _module.DisposeAsync();
    }
}
