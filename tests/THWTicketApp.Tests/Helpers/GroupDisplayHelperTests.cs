using Microsoft.JSInterop;
using NSubstitute;
using THWTicketApp.Shared.Models;
using THWTicketApp.Web.Helpers;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Helpers;

public class GroupDisplayHelperTests
{
    private static LocalizationService NewLocalization()
        => new(new LocalStorageService(Substitute.For<IJSRuntime>()));

    [Fact]
    public void GetDisplayName_returnsRawName_forNonPrivateGroup()
    {
        var group = new Group { Name = "TEST", Private = false };
        Assert.Equal("TEST", GroupDisplayHelper.GetDisplayName(group, NewLocalization()));
    }

    [Fact]
    public void GetDisplayName_returnsLocalizedLabel_forPrivateGroup_neverRawName()
    {
        // The raw Name of a private group is the internal
        // "private:<objectid>" identifier — it must never reach the UI.
        var group = new Group { Name = "private:507f1f77bcf86cd799439011", Private = true };
        var displayName = GroupDisplayHelper.GetDisplayName(group, NewLocalization());

        Assert.DoesNotContain("private:", displayName);
        Assert.Equal("🔒 Privater Bereich", displayName);
    }

    [Fact]
    public void GetDisplayName_returnsEmpty_forNullGroup()
    {
        Assert.Equal(string.Empty, GroupDisplayHelper.GetDisplayName(null, NewLocalization()));
    }

    [Fact]
    public void ExcludePrivate_filtersOutPrivateGroups()
    {
        var groups = new List<Group>
        {
            new() { Name = "Jugend", Private = false },
            new() { Name = "private:abc", Private = true },
            new() { Name = "Stab", Private = false },
        };

        var result = GroupDisplayHelper.ExcludePrivate(groups);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, g => g.Private);
    }
}
