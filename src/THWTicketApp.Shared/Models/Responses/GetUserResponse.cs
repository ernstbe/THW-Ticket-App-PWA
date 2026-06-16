namespace THWTicketApp.Shared.Models.Responses;

public class GetUserResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }

    // v1 endpoints (/users, /users/getassignees) wrap the list as "users";
    // v2 (/accounts) wraps it as "accounts". Both are deserialized and
    // UsersOrAccounts picks whichever is populated so callers stay agnostic.
    public List<User> Users { get; set; } = [];
    public List<User> Accounts { get; set; } = [];

    public List<User> UsersOrAccounts => Users.Count > 0 ? Users : Accounts;
}
