using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Revisión de una alerta: qué hizo el personal (RF08).
// Quién la atiende no viene aquí: la API lo toma de la sesión iniciada.
public class AtenderAlertaRequest
{
    [Required(ErrorMessage = "Describa las acciones realizadas.")]
    [StringLength(500, MinimumLength = 10,
        ErrorMessage = "La observación debe tener entre 10 y 500 caracteres.")]
    public string? Observacion { get; set; }
}
