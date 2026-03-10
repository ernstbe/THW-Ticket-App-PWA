using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using THWTicketApp.Shared.Services;
using THWTicketApp.Web;
using THWTicketApp.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// App settings
var settings = new AppSettings();
builder.Services.AddSingleton(settings);

// HTTP client
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Services
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<ITrueDeskApiService, TrueDeskApiService>();
builder.Services.AddScoped<AppStateService>();

// Auth
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddAuthorizationCore();

// MudBlazor
builder.Services.AddMudServices();

await builder.Build().RunAsync();
