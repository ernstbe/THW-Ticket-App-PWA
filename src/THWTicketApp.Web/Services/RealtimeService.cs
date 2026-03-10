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

    public event Action<string, string>? TicketEvent;
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
