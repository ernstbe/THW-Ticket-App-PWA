namespace THWTicketApp.Shared.Models.Responses;

public class GetTicketsResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public List<Ticket> Tickets { get; set; } = [];
}
