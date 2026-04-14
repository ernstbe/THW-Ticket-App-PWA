namespace THWTicketApp.Shared.Data;

public enum ConflictType
{
    None = 0,
    TicketUpdated,
    StatusChanged,
    TicketDeleted,
    PermissionRevoked
}
