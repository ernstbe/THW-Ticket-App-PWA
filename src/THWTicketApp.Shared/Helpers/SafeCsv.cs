namespace THWTicketApp.Shared.Helpers;

/// <summary>
/// CSV field formatting that is safe against spreadsheet formula injection.
/// A cell whose value begins with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c> (or a
/// tab/CR/LF) is interpreted as a formula when the file is opened in
/// Excel/LibreOffice. Since ticket subjects (etc.) are attacker-controllable,
/// such values are prefixed with a single quote so they render as text, and the
/// field is CSV-quoted (embedded quotes doubled).
/// </summary>
public static class SafeCsv
{
    public static string Field(string? value)
    {
        var s = value ?? string.Empty;
        if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
            s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
