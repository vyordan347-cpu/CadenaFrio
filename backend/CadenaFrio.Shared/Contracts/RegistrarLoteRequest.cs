using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Lecturas que el dispositivo guardó mientras no tenía conexión y envía juntas (HU06).
public class RegistrarLoteRequest
{
    [Required(ErrorMessage = "El lote debe contener lecturas.")]
    [MinLength(1, ErrorMessage = "El lote debe contener al menos una lectura.")]
    [MaxLength(500, ErrorMessage = "El lote no puede tener más de 500 lecturas.")]
    public List<LecturaLoteDto>? Lecturas { get; set; }
}

// Una lectura dentro del lote. No lleva validaciones automáticas a propósito:
// una lectura inválida no debe rechazar el lote completo, solo descartarse a sí misma.
public class LecturaLoteDto
{
    public double? Temperatura { get; set; }

    // Obligatoria en un lote: sin ella no se sabría cuándo se midió.
    public DateTime? FechaMedicion { get; set; }
}
