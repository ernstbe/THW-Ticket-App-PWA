namespace THWTicketApp.Shared.Services;

public class AppSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:8118/api/v2";
    public int ConnectionTimeoutSeconds { get; set; } = 30;
}
