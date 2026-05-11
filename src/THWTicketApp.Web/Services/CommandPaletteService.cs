namespace THWTicketApp.Web.Services;

/// <summary>
/// Coordinates the global Ctrl+K command palette. Pages register their
/// static actions once (typically in MainLayout's OnInitialized); the
/// palette component subscribes to <see cref="StateChanged"/> and re-
/// renders when the open state or query changes.
///
/// Singleton state at the page-tree level — there's only ever one
/// palette visible at a time.
/// </summary>
public sealed class CommandPaletteService
{
    public bool IsOpen { get; private set; }
    public string Query { get; private set; } = string.Empty;

    /// <summary>
    /// Static actions registered at app start. Dynamic results (ticket
    /// search, recently visited) are computed on demand by the palette
    /// component and not stored here.
    /// </summary>
    public List<PaletteAction> StaticActions { get; } = new();

    public event Action? StateChanged;

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Query = string.Empty;
        StateChanged?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Query = string.Empty;
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void SetQuery(string q)
    {
        Query = q ?? string.Empty;
        StateChanged?.Invoke();
    }

    public void Register(PaletteAction action)
    {
        // De-duplicate on Id so re-registration during hot-reload doesn't
        // produce duplicate entries.
        StaticActions.RemoveAll(a => a.Id == action.Id);
        StaticActions.Add(action);
    }

    public void RegisterMany(IEnumerable<PaletteAction> actions)
    {
        foreach (var a in actions) Register(a);
    }
}

public sealed class PaletteAction
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    /// <summary>Material icon path, e.g. Icons.Material.Filled.Add.</summary>
    public string? Icon { get; init; }
    /// <summary>Group heading shown above the result in the list (e.g. "Navigation", "Aktionen").</summary>
    public string? Category { get; init; }
    /// <summary>Searchable keywords beyond Title/Subtitle (e.g. ["new", "create", "neu"]).</summary>
    public string[]? Keywords { get; init; }
    /// <summary>Executed when the user selects this action. Service closes itself before invocation.</summary>
    public Func<Task>? ExecuteAsync { get; init; }

    /// <summary>
    /// Case-insensitive substring match against Title, Subtitle, and Keywords.
    /// Returns a small relevance score: 0 = no match, higher = better.
    /// </summary>
    public int MatchScore(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 1; // everything matches an empty query
        var q = query.Trim();

        int best = 0;
        if (Title.StartsWith(q, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 100);
        else if (Title.Contains(q, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 50);

        if (!string.IsNullOrEmpty(Subtitle))
        {
            if (Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 20);
        }
        if (Keywords != null)
        {
            foreach (var k in Keywords)
            {
                if (k.StartsWith(q, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 30);
                else if (k.Contains(q, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 10);
            }
        }
        return best;
    }
}
