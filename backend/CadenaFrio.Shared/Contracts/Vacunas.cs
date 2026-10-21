using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Vacuna del catálogo.
public class VacunaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }
    public string Fuente { get; set; } = string.Empty;
    public bool Activo { get; set; }

    // Cuántos equipos la guardan.
    public int CantidadEquipos { get; set; }
}

// Datos para crear o editar una vacuna del catálogo.
public class GuardarVacunaRequest : IValidatableObject
{
    private const string MensajeRangoFisico = "Debe estar entre -50 °C y 60 °C.";

    [Required(ErrorMessage = "Ingrese el nombre de la vacuna.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(LimitesFisicos.TemperaturaMinima, LimitesFisicos.TemperaturaMaxima, ErrorMessage = MensajeRangoFisico)]
    public double? OptimoMinimo { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(LimitesFisicos.TemperaturaMinima, LimitesFisicos.TemperaturaMaxima, ErrorMessage = MensajeRangoFisico)]
    public double? OptimoMaximo { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(LimitesFisicos.TemperaturaMinima, LimitesFisicos.TemperaturaMaxima, ErrorMessage = MensajeRangoFisico)]
    public double? LimiteMinimo { get; set; }

    [Required(ErrorMessage = "Obligatorio.")]
    [Range(LimitesFisicos.TemperaturaMinima, LimitesFisicos.TemperaturaMaxima, ErrorMessage = MensajeRangoFisico)]
    public double? LimiteMaximo { get; set; }

    [StringLength(200, ErrorMessage = "La fuente no puede superar 200 caracteres.")]
    public string? Fuente { get; set; }

    // Mismas reglas que los rangos de un equipo: límite mín. ≤ óptimo mín. < óptimo máx. ≤ límite máx.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OptimoMinimo >= OptimoMaximo)
        {
            yield return new ValidationResult("El óptimo mínimo debe ser menor que el óptimo máximo.",
                new[] { nameof(OptimoMinimo), nameof(OptimoMaximo) });
        }

        if (LimiteMinimo > OptimoMinimo)
        {
            yield return new ValidationResult("El límite mínimo no puede ser mayor que el óptimo mínimo.",
                new[] { nameof(LimiteMinimo) });
        }

        if (LimiteMaximo < OptimoMaximo)
        {
            yield return new ValidationResult("El límite máximo no puede ser menor que el óptimo máximo.",
                new[] { nameof(LimiteMaximo) });
        }
    }
}

// Equipo para listas de selección.
public class EquipoResumenDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoEquipoDto Tipo { get; set; }
}

// Vacunas que guarda un equipo.
public class AsignarVacunasRequest
{
    public List<int> VacunaIds { get; set; } = new();
}
