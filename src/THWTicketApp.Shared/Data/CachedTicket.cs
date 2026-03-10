namespace THWTicketApp.Shared.Data;

public class CachedTicket
{
    public string Id { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Issue { get; set; }
    public DateTime Date { get; set; }
    public DateTime Updated { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public int Uid { get; set; }
    public bool Deleted { get; set; }
    public string? StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusHtmlColor { get; set; }
    public bool StatusIsResolved { get; set; }
    public string? PriorityId { get; set; }
    public string? PriorityName { get; set; }
    public string? PriorityHtmlColor { get; set; }
    public string? TypeId { get; set; }
    public string? TypeName { get; set; }
    public string? OwnerId { get; set; }
    public string? OwnerFullname { get; set; }
    public string? OwnerEmail { get; set; }
    public string? AssigneeId { get; set; }
    public string? AssigneeFullname { get; set; }
    public string? AssigneeEmail { get; set; }
    public string? GroupId { get; set; }
    public string? GroupName { get; set; }
    public DateTime CachedAt { get; set; }
}
