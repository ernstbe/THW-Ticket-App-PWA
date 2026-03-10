namespace THWTicketApp.Shared.Data;

public class TimeEntry
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Description { get; set; }
    public double DurationMinutes => EndTime.HasValue
        ? (EndTime.Value - StartTime).TotalMinutes
        : (DateTime.UtcNow - StartTime).TotalMinutes;
}
