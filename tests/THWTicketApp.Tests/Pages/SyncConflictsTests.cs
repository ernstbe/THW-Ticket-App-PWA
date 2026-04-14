using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using THWTicketApp.Shared.Data;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class SyncConflictsTests : TestContext
{
    private readonly ISyncService _sync;

    public SyncConflictsTests()
    {
        _sync = Substitute.For<ISyncService>();
        _sync.GetConflictedActionsAsync().Returns(new List<PendingAction>());

        Services.AddMudServices();
        Services.AddSingleton(_sync);
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new AlwaysAuthenticatedProvider());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_successAlert_whenNoConflicts()
    {
        var cut = RenderComponent<SyncConflicts>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Keine Sync-Konflikte vorhanden", cut.Markup));
    }

    [Fact]
    public void Renders_oneCardPerConflict_withDataAttribute()
    {
        _sync.GetConflictedActionsAsync().Returns(new List<PendingAction>
        {
            new() { Id = 1, ActionType = "AddComment", ConflictType = ConflictType.TicketUpdated, ConflictReason = "r1", CreatedAt = DateTime.UtcNow, IsConflicted = true },
            new() { Id = 2, ActionType = "UpdateStatus", ConflictType = ConflictType.StatusChanged, ConflictReason = "r2", CreatedAt = DateTime.UtcNow, IsConflicted = true },
            new() { Id = 3, ActionType = "AddNote", ConflictType = ConflictType.TicketDeleted, ConflictReason = "r3", CreatedAt = DateTime.UtcNow, IsConflicted = true }
        });

        var cut = RenderComponent<SyncConflicts>();

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-conflict-type]");
            Assert.Equal(3, cards.Count);
            Assert.Equal("TicketUpdated", cards[0].GetAttribute("data-conflict-type"));
            Assert.Equal("StatusChanged", cards[1].GetAttribute("data-conflict-type"));
            Assert.Equal("TicketDeleted", cards[2].GetAttribute("data-conflict-type"));
        });
    }

    [Fact]
    public void Renders_translatesNewR21ActionTypes()
    {
        _sync.GetConflictedActionsAsync().Returns(new List<PendingAction>
        {
            new() { Id = 1, ActionType = "UpdateTicketFields", ConflictType = ConflictType.TicketUpdated, ConflictReason = "r", CreatedAt = DateTime.UtcNow, IsConflicted = true },
            new() { Id = 2, ActionType = "DeleteTicket", ConflictType = ConflictType.TicketDeleted, ConflictReason = "r", CreatedAt = DateTime.UtcNow, IsConflicted = true },
            new() { Id = 3, ActionType = "UploadAttachment", ConflictType = ConflictType.TicketUpdated, ConflictReason = "r", CreatedAt = DateTime.UtcNow, IsConflicted = true }
        });

        var cut = RenderComponent<SyncConflicts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ticket-Felder aktualisieren", cut.Markup);
            Assert.Contains("Ticket löschen", cut.Markup);
            Assert.Contains("Anhang hochladen", cut.Markup);
        });
    }

    // ---------------------------------------------------------------------
    // Branch-metadata unit tests — exercised directly without rendering
    // ---------------------------------------------------------------------

    [Fact]
    public void GetConflictBranch_TicketDeleted_disablesForceApply()
    {
        var branch = SyncConflicts.GetConflictBranch(ConflictType.TicketDeleted);
        Assert.False(branch.AllowForceApply);
        Assert.Equal(Severity.Error, branch.AlertSeverity);
        Assert.Contains("gelöscht", branch.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetConflictBranch_PermissionRevoked_disablesForceApply()
    {
        var branch = SyncConflicts.GetConflictBranch(ConflictType.PermissionRevoked);
        Assert.False(branch.AllowForceApply);
        Assert.Equal(Severity.Error, branch.AlertSeverity);
    }

    [Fact]
    public void GetConflictBranch_StatusChanged_allowsForceApplyAsWarning()
    {
        var branch = SyncConflicts.GetConflictBranch(ConflictType.StatusChanged);
        Assert.True(branch.AllowForceApply);
        Assert.Equal(Severity.Warning, branch.AlertSeverity);
    }

    [Fact]
    public void GetConflictBranch_TicketUpdated_allowsForceApplyAsWarning()
    {
        var branch = SyncConflicts.GetConflictBranch(ConflictType.TicketUpdated);
        Assert.True(branch.AllowForceApply);
        Assert.Equal(Severity.Warning, branch.AlertSeverity);
    }

    [Theory]
    [InlineData("UpdateTicketFields", "Ticket-Felder aktualisieren")]
    [InlineData("DeleteTicket", "Ticket löschen")]
    [InlineData("UploadAttachment", "Anhang hochladen")]
    [InlineData("AddComment", "Kommentar hinzufügen")]
    [InlineData("SomethingUnknown", "SomethingUnknown")]
    public void TranslateActionType_matchesExpectedGermanLabels(string actionType, string expected)
    {
        Assert.Equal(expected, SyncConflicts.TranslateActionType(actionType));
    }

    private sealed class AlwaysAuthenticatedProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "test")],
                "test");
            return Task.FromResult(new AuthenticationState(new System.Security.Claims.ClaimsPrincipal(identity)));
        }
    }
}
