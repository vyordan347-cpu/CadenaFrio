using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Datos que envía la pantalla de configuración para registrar un rango nuevo (HU02).
// Quién hace el cambio ya no viene aquí: la API lo toma del usuario con sesión iniciada.
// IValidatableObject permite validar reglas que involucran varios campos a la vez.
public class ConfigurarRangoRequest : IValidatableObject
{
    [Required(ErrorMessage = "La temperatura mínima es obligatoria.")]
    [Range(-50.0, 60.0, ErrorMessage = "La temperatura mínima debe estar entre -50 °C y 60 °C.")]
    public double? TemperaturaMinima { get; set; }

    [Required(ErrorMessage = "La temperatura máxima es obligatoria.")]
    [Range(-50.0, 60.0, ErrorMessage = "La temperatura máxima debe estar entre -50 °C y 60 °C.")]
    public double? TemperaturaMaxima { get; set; }

    // Regla entre campos: el mínimo debe ser menor que el máximo (RF04).
    // Solo se ejecuta si las validaciones de arriba ya pasaron.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TemperaturaMinima >= TemperaturaMaxima)
        {
            yield return new ValidationResult(
                "La temperatura mínima debe ser menor que la máxima.",
                new[] { nameof(TemperaturaMinima), nameof(TemperaturaMaxima) });
        }
    }
}
