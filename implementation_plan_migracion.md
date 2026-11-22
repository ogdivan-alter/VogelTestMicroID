# Plan de Migración: `VogelHosted` → `VogelT` (Blazor Web App `.NET 10`)

Este documento describe la migración del proyecto desde el modelo clásico **Blazor WebAssembly Hosted** (`VogelHosted`) hacia su equivalente oficial en **.NET 10**: **Blazor Web App** (`dotnet new blazor -int WebAssembly -ai`) con prerenderizado desactivado (`new InteractiveWebAssemblyRenderMode(prerender: false)`).

---

## 1. Decisiones de Diseño Aprobadas

1. **Nombre del proyecto anfitrión (Servidor)**: Se mantiene **`Vogel.Server`** (`Vogel.Server.csproj`) adoptando internamente la estructura de Blazor Web App (`Components/App.razor`, `MapStaticAssets()`, `MapRazorComponents<App>()`), preservando el 100% de compatibilidad con los namespaces `Vogel.Server.*` y con el proyecto de pruebas `Vogel.Tests`.
2. **Modo de renderizado interactivo sin prerenderizado**: Se utiliza `new InteractiveWebAssemblyRenderMode(prerender: false)` tanto en `<HeadOutlet>` como en `<Routes>` dentro de `Vogel.Server/Components/App.razor`.
3. **Versiones de paquetes**: Se mantiene `10.0.14` para `Microsoft.AspNetCore.Components.WebAssembly.Server` y `Microsoft.AspNetCore.Components.WebAssembly`, evitando las advertencias de vulnerabilidad `NU1903` presentes en la versión `10.0.12`.

---

## 2. Cambios Realizados por Proyecto

### 2.1. `Vogel.Server` (Anfitrión Blazor Web App)
- **`Vogel.Server.csproj`**:
  - Se añade `<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`.
- **`Components/App.razor`**:
  - Sustituye al antiguo `Vogel.Client/wwwroot/index.html`.
  - Incluye `<ResourcePreloader />`, `<ImportMap />`, hojas de estilo con `@Assets[...]` (`bootstrap.min.css`, `css/app.css`, `Vogel.Server.styles.css`), scripts JS de sesión e inactividad (`js/inactivity.js`, `js/sessionStorage.js`) y el runtime unificado `_framework/blazor.web.js`.
  - Declara `<HeadOutlet @rendermode="new InteractiveWebAssemblyRenderMode(prerender: false)" />` y `<Routes @rendermode="new InteractiveWebAssemblyRenderMode(prerender: false)" />`.
- **`Components/_Imports.razor` y `Components/Pages/Error.razor`**:
  - Componentes base del host de servidor de la plantilla Blazor Web App `.NET 10`.
- **`Program.cs`**:
  - Registra `builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents()` junto con Serilog, políticas de cookies seguras, `ServerSessionInfo` y controladores API con `GlobalExceptionFilter`.
  - Configura en el pipeline `UseSecurityHeaders()`, `UseCookiePolicy()`, `UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)`, `UseHttpsRedirection()`, `UseSerilogRequestLogging()`, `UseAntiforgery()`, `MapStaticAssets()`, `MapControllers()` y `MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(typeof(Vogel.Client._Imports).Assembly)`.

### 2.2. `Vogel.Client` (Cliente WebAssembly)
- **`Vogel.Client.csproj`**:
  - Configura `<NoDefaultLaunchSettingsFile>true</NoDefaultLaunchSettingsFile>`, `<StaticWebAssetProjectMode>Default</StaticWebAssetProjectMode>` y `<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`.
  - Retira `Microsoft.AspNetCore.Components.WebAssembly.DevServer` y `Properties/launchSettings.json`.
- **`Routes.razor` (antes `App.razor`)**:
  - Contiene la lógica de arranque seguro (`_ready`, validación de instancia de servidor en paralelo con restauración de sesión cifrada, `<CascadingAuthenticationState>`, `<Router>` y `<AuthGuard>`).
- **`Program.cs`**:
  - Retira `builder.RootComponents.Add<App>("#app")` y `builder.RootComponents.Add<HeadOutlet>("head::after")` (ahora gestionados por `Components/App.razor` a través de `blazor.web.js`).
  - Mantiene intacta toda la configuración de inyección de dependencias del cliente.
- **`wwwroot/`**:
  - Se elimina `index.html`; se mantienen todos los recursos estáticos y `appsettings.json`.
