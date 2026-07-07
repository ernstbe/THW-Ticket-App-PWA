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

        // Best-effort. requestPermission rejects when called outside a user
        // gesture on iOS Safari / installed PWAs (NotAllowedError), and
        // GetModuleAsync throws if the module fails to load (stale cache/404
        // after a deploy). This runs during MainLayout's first render BEFORE
        // offline detection, the sync timer, install handler, shortcuts and dark
        // mode are set up, so an uncaught JSException here would skip all of that
        // (#213). Swallow it — notifications are optional.
        try
        {
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
        catch (JSException)
        {
            // Notification permission is optional; never block layout init on it.
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

            // The body references the human-facing numeric uid, never the Mongo
            // _id (which is what ticketId holds). When the socket payload carried
            // no uid, fall back to the generic title text instead of leaking the
            // raw _id into the notification (#304).
            string Body(string key) => string.IsNullOrEmpty(ticketUid)
                ? _localization.T("notify.ticket_event")
                : string.Format(_localization.T(key), ticketUid);

            var (title, body) = eventName switch
            {
                "ticketCreated" => (_localization.T("notify.ticket_created"), Body("notify.ticket_created_body")),
                "ticketUpdated" => (_localization.T("notify.ticket_updated"), Body("notify.ticket_updated_body")),
                "statusUpdated" => (_localization.T("notify.status_changed"), Body("notify.status_changed_body")),
                "assigneeUpdated" => (_localization.T("notify.assignee_changed"), Body("notify.assignee_changed_body")),
                "commentNoteAdded" => (_localization.T("notify.comment_added"), Body("notify.comment_added_body")),
                _ => (_localization.T("notify.ticket_event"), Body("notify.ticket_event_body"))
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
