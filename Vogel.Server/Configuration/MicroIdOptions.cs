using System.ComponentModel.DataAnnotations;

namespace Vogel.Server.Configuration;

/// <summary>
/// Opciones de configuración para la integración del BFF con el servicio MicroID.
/// </summary>
public sealed class MicroIdOptions : IValidatableObject
{
    public const string SectionName = "MicroId";

    private static readonly HashSet<string> AllowedRenovations = new(StringComparer.Ordinal)
    {
        "MONTHLY",
        "ANNUAL",
        "TEMPORARY"
    };

    /// <summary>
    /// URL base del servicio MicroID (ej. http://localhost:8021 o https://microid.dominio.com).
    /// </summary>
    [Required(ErrorMessage = "MicroId:BaseUrl es obligatorio.")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Identificador textual del sistema cliente enviado en la cabecera X-System-Id.
    /// </summary>
    [Required(ErrorMessage = "MicroId:SystemId es obligatorio.")]
    [MaxLength(64)]
    public string SystemId { get; set; } = "MICROID";

    /// <summary>
    /// Identificador numérico del sistema en MicroID (id_system para /creds/create).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "MicroId:SystemNumericId debe ser mayor a 0.")]
    public int SystemNumericId { get; set; } = 1;

    /// <summary>
    /// Tiempo máximo de espera en segundos para las peticiones HTTP hacia MicroID.
    /// </summary>
    [Range(1, 300, ErrorMessage = "MicroId:TimeoutSeconds debe estar entre 1 y 300 segundos.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Política de renovación de la llave de sistema en /creds/create (MONTHLY | ANNUAL | TEMPORARY).
    /// </summary>
    [Required(ErrorMessage = "MicroId:CredRenovation es obligatorio.")]
    public string CredRenovation { get; set; } = "MONTHLY";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "MicroId:BaseUrl debe ser una URL HTTP/HTTPS absoluta válida.",
                [nameof(BaseUrl)]);
        }
        else if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        {
            yield return new ValidationResult(
                "MicroId:BaseUrl debe usar HTTPS cuando no apunta a una dirección loopback.",
                [nameof(BaseUrl)]);
        }

        if (!AllowedRenovations.Contains(CredRenovation))
        {
            yield return new ValidationResult(
                "MicroId:CredRenovation debe ser uno de los valores permitidos: MONTHLY, ANNUAL o TEMPORARY.",
                [nameof(CredRenovation)]);
        }
    }
}
