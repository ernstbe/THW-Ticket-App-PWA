using Microsoft.AspNetCore.Components;
using THWTicketApp.Web.Services;

namespace THWTicketApp.Tests.Services;

/// <summary>
/// Verifies that the Ticket-Detail back button follows the last "list"
/// page the user actually visited (Dashboard, Kanban, filtered Tickets)
/// instead of defaulting to /tickets.
/// </summary>
public class NavigationHistoryServiceTests
{
    [Fact]
    public void InitialUri_root_isRemembered()
    {
        var nav = new TestNavigationManager("https://test/");
        var sut = new NavigationHistoryService(nav);
        Assert.Equal(string.Empty, sut.LastListUri);
    }

    [Fact]
    public void InitialUri_ticketDetail_fallsBackToTicketsList()
    {
        var nav = new TestNavigationManager("https://test/tickets/42");
        var sut = new NavigationHistoryService(nav);
        Assert.Equal("tickets", sut.LastListUri);
    }

    [Fact]
    public void Navigating_toKanban_thenDetail_remembersKanban()
    {
        var nav = new TestNavigationManager("https://test/");
        var sut = new NavigationHistoryService(nav);

        nav.NavigateTo("kanban");
        nav.NavigateTo("tickets/123");

        Assert.Equal("kanban", sut.LastListUri);
    }

    [Fact]
    public void FilteredTicketsList_preservesQueryString()
    {
        var nav = new TestNavigationManager("https://test/");
        var sut = new NavigationHistoryService(nav);

        nav.NavigateTo("tickets?filter=overdue");
        nav.NavigateTo("tickets/7");

        Assert.Equal("tickets?filter=overdue", sut.LastListUri);
    }

    [Fact]
    public void TicketDetailNavigation_doesNotOverwriteLastList()
    {
        var nav = new TestNavigationManager("https://test/");
        var sut = new NavigationHistoryService(nav);

        nav.NavigateTo("tickets");
        nav.NavigateTo("tickets/1");
        nav.NavigateTo("tickets/2");

        Assert.Equal("tickets", sut.LastListUri);
    }

    [Fact]
    public void Dispose_unsubscribesFromLocationChanged()
    {
        var nav = new TestNavigationManager("https://test/");
        var sut = new NavigationHistoryService(nav);

        nav.NavigateTo("kanban");
        sut.Dispose();
        nav.NavigateTo("templates");

        Assert.Equal("kanban", sut.LastListUri);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string initialUri) => Initialize("https://test/", initialUri);
        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            var newUri = new Uri(new Uri(BaseUri), uri).ToString();
            Uri = newUri;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }
}
