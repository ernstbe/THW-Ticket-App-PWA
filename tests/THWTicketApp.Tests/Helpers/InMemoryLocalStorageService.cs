using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Helpers;

/// <summary>
/// In-memory <see cref="LocalStorageService"/> for unit tests. Bypasses
/// the JS interop layer entirely — every Get/Set/Remove operates on a
/// plain <see cref="Dictionary{TKey,TValue}"/>.
///
/// Made possible by LocalStorageService's methods being virtual.
/// </summary>
public sealed class InMemoryLocalStorageService : LocalStorageService
{
    public readonly Dictionary<string, string> Store = new();

    public InMemoryLocalStorageService()
        : base(Substitute.For<IJSRuntime>())
    {
    }

    public override Task<string?> GetItemAsync(string key)
        => Task.FromResult(Store.TryGetValue(key, out var v) ? v : null);

    public override Task SetItemAsync(string key, string value)
    {
        Store[key] = value;
        return Task.CompletedTask;
    }

    public override Task RemoveItemAsync(string key)
    {
        Store.Remove(key);
        return Task.CompletedTask;
    }
}
