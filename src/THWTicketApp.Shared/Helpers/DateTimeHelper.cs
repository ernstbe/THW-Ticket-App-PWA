namespace THWTicketApp.Shared.Helpers;

public static class DateTimeHelper
{
    public static string ToLocalDisplayString(this DateTime dt, string format)
    {
        if (dt == default) return string.Empty;
        var utc = dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
        return utc.ToLocalTime().ToString(format);
    }

    public static string ToLocalDisplayString(this DateTime? dt, string format)
        => dt.HasValue ? dt.Value.ToLocalDisplayString(format) : string.Empty;
}
