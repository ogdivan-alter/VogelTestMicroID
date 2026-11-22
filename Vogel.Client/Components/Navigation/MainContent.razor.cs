using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente MainContent.
/// Despliega el contenido de la página principal: una vista personalizada (@customView) o
/// el Jumbotron por defecto parametrizado desde appsettings.json.
/// Corresponde a la sección 2.3.5 del Requerimiento Maestro.
/// </summary>
public class MainContentBase : ComponentBase
{
    [Inject] protected IConfiguration Configuration { get; set; } = default!;

    /// <summary>
    /// Fragmento para renderizar una vista personalizada en lugar del Jumbotron por defecto.
    /// </summary>
    [Parameter] public RenderFragment? CustomView { get; set; }

    /// <summary>
    /// Ruta de la imagen de logo para la aplicación.
    /// </summary>
    [Parameter] public string? MainImage { get; set; }

    /// <summary>
    /// Texto de bienvenida o título principal a desplegar en el Jumbotron.
    /// </summary>
    [Parameter] public string? MainText { get; set; }

    protected override void OnInitialized()
    {
        // Si no se proporcionaron parámetros explícitos y no hay vista personalizada,
        // inicializar desde appsettings.json
        if (CustomView == null)
        {
            if (string.IsNullOrEmpty(MainImage))
            {
                MainImage = Configuration.GetValue<string>("MainView:MainImage", "images/logo.svg") ?? "images/logo.svg";
            }

            if (string.IsNullOrEmpty(MainText))
            {
                MainText = Configuration.GetValue<string>("MainView:MainText", "Bienvenido a la Plataforma Vogel") ?? "Bienvenido a la Plataforma Vogel";
            }
        }
    }
}
