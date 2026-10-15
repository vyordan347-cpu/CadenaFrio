using System.ComponentModel.DataAnnotations;
 
namespace CadenaFrio.Shared.Contracts;
 
// Datos que envía la pantalla de configuración para registrar un rango nuevo (HU02).
// IValidatableObject permite validar reglas que involucran varios campos a la vez.
public class ConfigurarRangoRequest : IValidatableObject
{
    [Required(ErrorMessage = "La temperatura mínima es obligatoria.")]
    [Range(-50.0, 60.0, ErrorMessage = "La temperatura mínima debe estar entre -50 °C y 60 °C.")]
    public double? TemperaturaMinima { get; set; }
 
    [Required(ErrorMessage = "La temperatura máxima es obligatoria.")]
    [Range(-50.0, 60.0, ErrorMessage = "La temperatura máxima debe estar entre -50 °C y 60 °C.")]
    public double? TemperaturaMaxima { get; set; }
 
    // Temporal: cuando exista el inicio de sesión, se tomará del usuario autenticado.
    [Required(ErrorMessage = "Indique quién realiza el cambio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    public string? ModificadoPor { get; set; }
 
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
 