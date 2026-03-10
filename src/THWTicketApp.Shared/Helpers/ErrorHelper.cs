using System.Net;

namespace THWTicketApp.Shared.Helpers;

public static class ErrorHelper
{
    public static (string Message, bool CanRetry) Categorize(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Unauthorized
                => ("Sitzung abgelaufen. Bitte erneut anmelden.", false),
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Forbidden
                => ("Keine Berechtigung für diese Aktion.", false),
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.NotFound
                => ("Ressource nicht gefunden.", false),
            HttpRequestException httpEx when httpEx.StatusCode >= HttpStatusCode.InternalServerError
                => ("Serverfehler. Bitte später erneut versuchen.", true),
            HttpRequestException
                => ("Verbindung zum Server fehlgeschlagen. Netzwerk prüfen.", true),
            TaskCanceledException
                => ("Zeitüberschreitung. Server antwortet nicht.", true),
            System.Text.Json.JsonException
                => ("Ungültiges Datenformat vom Server.", false),
            _ => ("Ein unerwarteter Fehler ist aufgetreten.", true)
        };
    }
}
