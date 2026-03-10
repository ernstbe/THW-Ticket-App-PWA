namespace THWTicketApp.Shared.Data;

public class SavedFilter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
