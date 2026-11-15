namespace THWTicketApp.Shared.Models;

/// <summary>
/// #mailnotify: the signed-in user's e-mail notification switches, as
/// returned by <c>GET /api/v2/accounts/me/notification-settings</c>.
/// <see cref="Personal"/> covers comments on own or assigned tickets,
/// @mentions and assignments; <see cref="NewTickets"/> every new ticket in
/// the user's groups. <see cref="MailEnabled"/> is false when the server
/// cannot send mail at all, in which case the switches are pointless.
/// </summary>
public record EmailNotificationSettings(bool Personal, bool NewTickets, bool MailEnabled);
