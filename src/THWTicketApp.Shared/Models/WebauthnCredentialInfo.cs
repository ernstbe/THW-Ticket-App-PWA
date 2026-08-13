namespace THWTicketApp.Shared.Models;

/// <summary>
/// A registered WebAuthn credential as listed in Settings — no key
/// material (publicKey/counter), the server never sends that to the client.
/// </summary>
public class WebauthnCredentialInfo
{
    public string CredentialId { get; set; } = string.Empty;
    public string? DeviceLabel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}
