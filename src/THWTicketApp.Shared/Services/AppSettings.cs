namespace THWTicketApp.Shared.Services;

public class AppSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiBaseUrl);
}
