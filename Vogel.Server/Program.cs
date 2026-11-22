using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.Extensions.Options;
using Serilog;
using Vogel.Server.Components;
using Vogel.Server.Configuration;
using Vogel.Server.Filters;
using Vogel.Server.Middleware;
using Vogel.Server.Services;
using Vogel.Server.Services.MicroId;
using Vogel.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuración de Serilog para registro estructurado.
// Los niveles mínimos, sinks (consola/archivo), rotación diaria y retención
// se definen en la sección "Serilog" de appsettings.json.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Configuración de cookies seguras (OWASP / CWE)
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
});

// Información de instancia del servidor para invalidación de sesiones al reiniciarse
builder.Services.AddSingleton<ServerSessionInfo>();

// Servicio de datos simulados local (soporte para modo Mock)
builder.Services.AddSingleton<IMockDataService, MockDataService>();

// Configuración y clientes HTTP para integración con MicroID
builder.Services.AddOptions<MicroIdOptions>()
    .Bind(builder.Configuration.GetSection(MicroIdOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient(MicroIdSystemKeyProvider.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<MicroIdOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
});

builder.Services.AddSingleton<IMicroIdSystemKeyProvider, MicroIdSystemKeyProvider>();

builder.Services.AddHttpClient<IMicroIdApiClient, MicroIdApiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<MicroIdOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
});

builder.Services.AddScoped<IMicroIdAuthService, MicroIdAuthService>();

// Cliente HTTP del BFF hacia el Backend de negocio y autenticación externo
var apiBackendBaseUrl = builder.Configuration["ApiBackend:BaseUrl"] ?? "https://localhost:7113";
builder.Services.AddHttpClient<IBackendApiService, BackendApiService>(client =>
{
    var baseUrl = apiBackendBaseUrl.TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
});

// Servicios de soporte de controladores API con filtro global de excepciones
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
});

// Servicios de Blazor Web App con soporte de componentes interactivos WebAssembly
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Inyección de Cabeceras HTTP de Seguridad (CSP, X-Frame-Options, etc.)
app.UseSecurityHeaders();

// Aplicación de políticas de cookies
app.UseCookiePolicy();

// Configuración del pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Registro estructurado y resumido de cada solicitud HTTP (una línea por request)
app.UseSerilogRequestLogging();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Vogel.Client._Imports).Assembly);

app.Run();
