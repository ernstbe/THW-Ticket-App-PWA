namespace THWTicketApp.Web.Services;

/// <summary>
/// Tracks whether the user has completed (or explicitly skipped) the
/// onboarding tour shown after their first login.
///
/// Stored locally per device under "onboarding_completed". Anyone who
/// installs the app fresh sees the tour once; thereafter they only
/// see it again if they reset it from Settings.
/// </summary>
public sealed class OnboardingService
{
    public const string StorageKey = "onboarding_completed";

    private readonly LocalStorageService _localStorage;
    public bool IsVisible { get; private set; }
    public event Action? StateChanged;

    public OnboardingService(LocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    /// <summary>
    /// Called from MainLayout after auth. Opens the tour exactly once
    /// per device unless <see cref="ResetAsync"/> was called.
    /// </summary>
    public async Task ShowIfFirstRunAsync()
    {
        var done = await _localStorage.GetItemAsync(StorageKey);
        if (done == "true") return;
        IsVisible = true;
        StateChanged?.Invoke();
    }

    /// <summary>Open the tour regardless of stored state (Settings "view again" button).</summary>
    public void ShowNow()
    {
        IsVisible = true;
        StateChanged?.Invoke();
    }

    public async Task CompleteAsync()
    {
        IsVisible = false;
        await _localStorage.SetItemAsync(StorageKey, "true");
        StateChanged?.Invoke();
    }

    public async Task SkipAsync() => await CompleteAsync();

    /// <summary>Clears the completion flag so the next mount shows the tour again.</summary>
    public async Task ResetAsync()
    {
        await _localStorage.RemoveItemAsync(StorageKey);
    }

    public async Task<bool> HasCompletedAsync()
    {
        var done = await _localStorage.GetItemAsync(StorageKey);
        return done == "true";
    }
}
