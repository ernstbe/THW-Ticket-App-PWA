namespace THWTicketApp.Web.Services;

public class AppStateService
{
    public event Action? OnChange;

    public bool IsOnline { get; set; } = true;
    public int PendingActionsCount { get; set; }
    public int UnreadNotificationCount { get; set; }

    public void NotifyStateChanged() => OnChange?.Invoke();
}
