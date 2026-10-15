using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Datos que envía la pantalla de configuración para registrar nuevos rangos (HU02).
// Quién hace el cambio no viene aquí: la API lo toma del usuario con sesión iniciada.
public class ConfigurarRangoRequest : IValidatableObject
{
    private const string MensajeRangoFisico = "Debe estar entre -50 °C y 60 °C.";

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(-50.0, 60.0, ErrorMessage = MensajeRangoFisico)]
    public double? OptimoMinimo { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(-50.0, 60.0, ErrorMessage = MensajeRangoFisico)]
    public double? OptimoMaximo { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(-50.0, 60.0, ErrorMessage = MensajeRangoFisico)]
    public double? LimiteMinimo { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(-50.0, 60.0, ErrorMessage = MensajeRangoFisico)]
    public double? LimiteMaximo { get; set; }

    // Reglas entre campos (RF04):
    // límite mínimo ≤ óptimo mínimo < óptimo máximo ≤ límite máximo.
    // Se permite igualar el óptimo con el límite cuando un lado no necesita franja de advertencia.
    // Solo se ejecuta si las validaciones de arriba ya pasaron.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OptimoMinimo >= OptimoMaximo)
        {
            yield return new ValidationResult(
                "El óptimo mínimo debe ser menor que el óptimo máximo.",
                new[] { nameof(OptimoMinimo), nameof(OptimoMaximo) });
        }

        if (LimiteMinimo > OptimoMinimo)
        {
            yield return new ValidationResult(
                "El límite mínimo no puede ser mayor que el óptimo mínimo.",
                new[] { nameof(LimiteMinimo) });
        }

        if (LimiteMaximo < OptimoMaximo)
        {
            yield return new ValidationResult(
                "El límite máximo no puede ser menor que el óptimo máximo.",
                new[] { nameof(LimiteMaximo) });
        }
    }
}
