namespace THWTicketApp.Shared.Models.Responses;

public class GetUserResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public List<User> Users { get; set; } = [];
}
