using Microsoft.JSInterop;

namespace THWTicketApp.Web.Services;

public class LocalStorageService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/storage-interop.js");
        return _module;
    }

    public async Task<string?> GetItemAsync(string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getItem", key);
    }

    public async Task SetItemAsync(string key, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setItem", key, value);
    }

    public async Task RemoveItemAsync(string key)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("removeItem", key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
