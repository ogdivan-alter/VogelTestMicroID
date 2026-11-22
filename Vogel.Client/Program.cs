using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Vogel.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// HttpClient para comunicación con el BFF (Vogel.Server)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<Vogel.Shared.Services.IMockDataService, Vogel.Shared.Services.MockDataService>();
builder.Services.AddScoped<ISecureSessionStorageService, SecureSessionStorageService>();
builder.Services.AddScoped<IClientLogger, ClientLogger>();
builder.Services.AddScoped<SessionState>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ISessionValidationService, SessionValidationService>();

// Soporte de Autorización nativa de Blazor (AuthorizeView)
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, CustomAuthenticationStateProvider>();

await builder.Build().RunAsync();
