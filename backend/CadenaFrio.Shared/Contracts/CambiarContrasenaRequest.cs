using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Cambio de contraseña por el propio usuario (obligatorio si tiene una temporal).
public class CambiarContrasenaRequest : IValidatableObject
{
    [Required(ErrorMessage = "Ingrese su contraseña actual.")]
    public string? ContrasenaActual { get; set; }

    [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres.")]
    public string? ContrasenaNueva { get; set; }

    [Required(ErrorMessage = "Repita la nueva contraseña.")]
    public string? Confirmacion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var nueva = ContrasenaNueva ?? string.Empty;

        if (!nueva.Any(char.IsLetter) || !nueva.Any(char.IsDigit))
        {
            yield return new ValidationResult(
                "La nueva contraseña debe tener al menos una letra y un número.",
                new[] { nameof(ContrasenaNueva) });
        }

        if (nueva == ContrasenaActual)
        {
            yield return new ValidationResult(
                "La nueva contraseña debe ser distinta de la actual.",
                new[] { nameof(ContrasenaNueva) });
        }

        if (nueva != Confirmacion)
        {
            yield return new ValidationResult(
                "Las contraseñas no coinciden.",
                new[] { nameof(Confirmacion) });
        }
    }
}
