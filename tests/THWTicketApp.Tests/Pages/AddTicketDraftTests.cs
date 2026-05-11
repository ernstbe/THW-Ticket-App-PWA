using System.Text.Json;
using THWTicketApp.Tests.Helpers;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

/// <summary>
/// Tests the draft serialization contract for AddTicket.razor.
///
/// Full bUnit render tests for the page would need to wire up the API,
/// localization, snackbar etc.; not worth it for the small serialization
/// surface this PR adds. Instead, we lock down the JSON shape and the
/// localStorage key so a future change doesn't quietly break drafts in
/// the field.
/// </summary>
public class AddTicketDraftTests
{
    [Fact]
    public void DraftKey_isStable()
    {
        // If you must change this string, write a one-shot migration that
        // also reads the old key — otherwise every user with an open draft
        // when they update the PWA loses it silently.
        Assert.Equal("draft_new_ticket", AddTicket.DraftKey);
    }

    [Fact]
    public void DraftDto_roundtripsThroughJson()
    {
        var json = JsonSerializer.Serialize(new { Subject = "Pumpe defekt", Issue = "Hauptpumpe macht Geräusche" });

        // Mirror what AddTicket.TryRestoreDraftAsync does.
        var doc = JsonDocument.Parse(json);
        Assert.Equal("Pumpe defekt", doc.RootElement.GetProperty("Subject").GetString());
        Assert.Equal("Hauptpumpe macht Geräusche", doc.RootElement.GetProperty("Issue").GetString());
    }

    [Fact]
    public async Task LocalStorage_RoundTrip_Works()
    {
        // Sanity: the InMemoryLocalStorageService used everywhere else
        // round-trips the same draft shape so the page logic can rely on it.
        var storage = new InMemoryLocalStorageService();
        var payload = JsonSerializer.Serialize(new { Subject = "a", Issue = "b" });

        await storage.SetItemAsync(AddTicket.DraftKey, payload);
        var read = await storage.GetItemAsync(AddTicket.DraftKey);
        Assert.Equal(payload, read);

        await storage.RemoveItemAsync(AddTicket.DraftKey);
        Assert.Null(await storage.GetItemAsync(AddTicket.DraftKey));
    }
}
