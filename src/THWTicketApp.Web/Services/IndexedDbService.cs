using Microsoft.JSInterop;

namespace THWTicketApp.Web.Services;

public class IndexedDbService : IIndexedDbService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public IndexedDbService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/indexeddb-interop.js");
        return _module;
    }

    public async Task SaveTicketsAsync(string ticketsJson)
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("saveTickets", ticketsJson);
    }

    public async Task<string> GetTicketsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("getTickets");
    }

    public async Task<int> GetCachedTicketCountAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<int>("getCachedTicketCount");
    }

    public async Task ClearTicketCacheAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("clearTicketCache");
    }

    public async Task EnqueuePendingActionAsync(string actionJson)
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("enqueuePendingAction", actionJson);
    }

    public async Task<string> GetPendingActionsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("getPendingActions");
    }

    public async Task<int> GetPendingActionCountAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<int>("getPendingActionCount");
    }

    public async Task RemovePendingActionAsync(int id)
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("removePendingAction", id);
    }

    public async Task ClearPendingActionsAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("clearPendingActions");
    }

    public async Task<bool> MarkActionConflictedAsync(int id, string reason)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("markActionConflicted", id, reason);
    }

    public async Task<string> GetConflictedActionsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("getConflictedActions");
    }

    public async Task<bool> UpdateRetryStateAsync(int id, string nextRetryAtIso, int retryCount, string? errorMessage)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("updateRetryState", id, nextRetryAtIso, retryCount, errorMessage);
    }

    public async Task AppendSyncLogAsync(string entryJson)
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("appendSyncLog", entryJson);
    }

    public async Task<string> GetSyncLogAsync(int limit = 0)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("getSyncLog", limit);
    }

    public async Task ClearSyncLogAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeAsync<bool>("clearSyncLog");
    }

    public async Task<string?> GetLastCacheTimeAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getLastCacheTime");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
            await _module.DisposeAsync();
    }
}
