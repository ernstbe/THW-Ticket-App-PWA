using Microsoft.JSInterop;

namespace THWTicketApp.Web.Services;

public class BrowserNotificationService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly LocalStorageService _localStorage;
    private readonly RealtimeService _realtimeService;
    private IJSObjectReference? _module;
    private bool _initialized;

    public BrowserNotificationService(IJSRuntime jsRuntime, LocalStorageService localStorage, RealtimeService realtimeService)
    {
        _jsRuntime = jsRuntime;
        _localStorage = localStorage;
        _realtimeService = realtimeService;
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

    private async void OnTicketEvent(string eventName, string ticketId)
    {
        try
        {
            var enabled = await _localStorage.GetItemAsync("settings_notifications");
            if (enabled != "true") return;

            // Check specific notification settings
            if (!await ShouldNotify(eventName)) return;

            var (title, body) = eventName switch
            {
                "ticketCreated" => ("Neues Ticket", $"Ticket #{ticketId} wurde erstellt."),
                "ticketUpdated" => ("Ticket aktualisiert", $"Ticket #{ticketId} wurde geändert."),
                "statusUpdated" => ("Status geändert", $"Status von Ticket #{ticketId} wurde geändert."),
                "assigneeUpdated" => ("Zuweisung geändert", $"Zuweisung von Ticket #{ticketId} wurde geändert."),
                "commentNoteAdded" => ("Neuer Kommentar", $"Neuer Kommentar bei Ticket #{ticketId}."),
                _ => ("Ticket-Ereignis", $"Änderung bei Ticket #{ticketId}.")
            };

            var url = string.IsNullOrEmpty(ticketId) ? null : $"/tickets/{ticketId}";
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
