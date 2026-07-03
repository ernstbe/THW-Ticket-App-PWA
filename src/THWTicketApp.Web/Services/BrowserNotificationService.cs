using Microsoft.JSInterop;

namespace THWTicketApp.Web.Services;

public class BrowserNotificationService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly LocalStorageService _localStorage;
    private readonly RealtimeService _realtimeService;
    private readonly LocalizationService _localization;
    private IJSObjectReference? _module;
    private bool _initialized;

    public BrowserNotificationService(IJSRuntime jsRuntime, LocalStorageService localStorage, RealtimeService realtimeService, LocalizationService localization)
    {
        _jsRuntime = jsRuntime;
        _localStorage = localStorage;
        _realtimeService = realtimeService;
        _localization = localization;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/notification-interop.js");
        return _module;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        _realtimeService.TicketEvent += OnTicketEvent;

        var module = await GetModuleAsync();
        var isSupported = await module.InvokeAsync<bool>("isSupported");
        if (!isSupported) return;

        var permission = await module.InvokeAsync<string>("getPermission");
        if (permission == "default")
        {
            var enabled = await _localStorage.GetItemAsync("settings_notifications");
            if (enabled == "true")
                await module.InvokeAsync<string>("requestPermission");
        }
    }

    public async Task<string> RequestPermissionAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("requestPermission");
    }

    private async void OnTicketEvent(string eventName, string ticketId, string ticketUid)
    {
        try
        {
            var enabled = await _localStorage.GetItemAsync("settings_notifications");
            if (enabled != "true") return;

            // Check specific notification settings
            if (!await ShouldNotify(eventName)) return;

            var (title, body) = eventName switch
            {
                "ticketCreated" => (_localization.T("notify.ticket_created"), string.Format(_localization.T("notify.ticket_created_body"), ticketId)),
                "ticketUpdated" => (_localization.T("notify.ticket_updated"), string.Format(_localization.T("notify.ticket_updated_body"), ticketId)),
                "statusUpdated" => (_localization.T("notify.status_changed"), string.Format(_localization.T("notify.status_changed_body"), ticketId)),
                "assigneeUpdated" => (_localization.T("notify.assignee_changed"), string.Format(_localization.T("notify.assignee_changed_body"), ticketId)),
                "commentNoteAdded" => (_localization.T("notify.comment_added"), string.Format(_localization.T("notify.comment_added_body"), ticketId)),
                _ => (_localization.T("notify.ticket_event"), string.Format(_localization.T("notify.ticket_event_body"), ticketId))
            };

            // Deep-link with the NUMERIC uid: /app/tickets/{TicketUid:int} can't
            // match the Mongo _id, so a _id-based URL always resolved to NotFound
            // and the notification never opened the ticket (#209).
            var url = string.IsNullOrEmpty(ticketUid) ? null : $"/app/tickets/{ticketUid}";
            var module = await GetModuleAsync();
            await module.InvokeAsync<bool>("showNotification", title, body, $"ticket-{ticketId}", url);
        }
        catch { }
    }

    private async Task<bool> ShouldNotify(string eventName)
    {
        return eventName switch
        {
            "ticketCreated" => await _localStorage.GetItemAsync("settings_notify_new") != "false",
            "commentNoteAdded" => await _localStorage.GetItemAsync("settings_notify_comments") != "false",
            "statusUpdated" => await _localStorage.GetItemAsync("settings_notify_status") != "false",
            _ => true
        };
    }

    public async ValueTask DisposeAsync()
    {
        _realtimeService.TicketEvent -= OnTicketEvent;
        if (_module != null)
            await _module.DisposeAsync();
    }
}
