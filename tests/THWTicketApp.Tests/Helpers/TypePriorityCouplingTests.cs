using THWTicketApp.Shared.Helpers;
using THWTicketApp.Shared.Models;

namespace THWTicketApp.Tests.Helpers;

public class TypePriorityCouplingTests
{
    private static readonly Priority Normal = new() { Id = "p1", Name = "Normal" };
    private static readonly Priority High = new() { Id = "p2", Name = "High" };
    private static readonly Priority VeryHigh = new() { Id = "p3", Name = "Very High" };
    private static readonly List<Priority> Union = [Normal, High, VeryHigh];

    // -----------------------------------------------------------------
    // PrioritiesForType
    // -----------------------------------------------------------------

    [Fact]
    public void PrioritiesForType_typeWithOwnPriorities_returnsOnlyThose()
    {
        var type = new TicketType { Id = "t1", Priorities = [Normal, High] };

        var result = TypePriorityCoupling.PrioritiesForType(type, Union);

        Assert.Equal(new[] { "p1", "p2" }, result.Select(p => p.Id));
    }

    [Fact]
    public void PrioritiesForType_nullType_fallsBackToUnion()
    {
        var result = TypePriorityCoupling.PrioritiesForType(null, Union);

        Assert.Equal(Union.Select(p => p.Id), result.Select(p => p.Id));
    }

    [Fact]
    public void PrioritiesForType_typeWithoutPriorities_fallsBackToUnion()
    {
        var type = new TicketType { Id = "t1", Priorities = [] };

        var result = TypePriorityCoupling.PrioritiesForType(type, Union);

        Assert.Equal(Union.Select(p => p.Id), result.Select(p => p.Id));
    }

    // -----------------------------------------------------------------
    // EnsureSelectedId (RecurringTasks: string-id binding)
    // -----------------------------------------------------------------

    [Fact]
    public void EnsureSelectedId_stillAvailable_keepsSelection()
    {
        Assert.Equal("p2", TypePriorityCoupling.EnsureSelectedId([Normal, High], "p2"));
    }

    [Fact]
    public void EnsureSelectedId_noLongerAvailable_clearsSelection()
    {
        Assert.Null(TypePriorityCoupling.EnsureSelectedId([Normal, High], "p3"));
    }

    [Fact]
    public void EnsureSelectedId_nullSelection_staysNull()
    {
        Assert.Null(TypePriorityCoupling.EnsureSelectedId([Normal, High], null));
    }

    // -----------------------------------------------------------------
    // EnsureSelected (AddTicket: object-reference binding)
    // -----------------------------------------------------------------

    [Fact]
    public void EnsureSelected_remapsToInstanceFromNewList()
    {
        // MudSelect matches items by reference — the selection must be the
        // instance contained in the rendered list, not a stale duplicate.
        var duplicateHigh = new Priority { Id = "p2", Name = "High" };
        List<Priority> available = [Normal, High];

        var result = TypePriorityCoupling.EnsureSelected(available, duplicateHigh);

        Assert.Same(High, result);
    }

    [Fact]
    public void EnsureSelected_notAvailable_returnsNull()
    {
        Assert.Null(TypePriorityCoupling.EnsureSelected([Normal, High], VeryHigh));
    }

    [Fact]
    public void EnsureSelected_nullSelection_returnsNull()
    {
        Assert.Null(TypePriorityCoupling.EnsureSelected([Normal, High], null));
    }
}
