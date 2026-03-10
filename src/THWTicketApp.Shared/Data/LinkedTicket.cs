namespace THWTicketApp.Shared.Data;

public class LinkedTicket
{
    public int Id { get; set; }
    public string SourceTicketId { get; set; } = string.Empty;
    public string LinkedTicketId { get; set; } = string.Empty;
    public string LinkedTicketSubject { get; set; } = string.Empty;
    public int LinkedTicketUid { get; set; }
    public string LinkType { get; set; } = "related";
    public DateTime CreatedAt { get; set; }
}
