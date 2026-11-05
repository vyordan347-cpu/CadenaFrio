using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Revisión de una alerta: qué hizo el personal (RF08).
// Quién la atiende no viene aquí: la API lo toma de la sesión iniciada.
// - Ruptura de temperatura: se llenan los campos del Anexo N° 13; la observación
//   es opcional y va como "Comentarios".
// - Silencio prolongado: solo la observación, obligatoria (la API lo verifica,
//   porque depende del tipo de alerta).
public class AtenderAlertaRequest
{
    [StringLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; set; }

    // Códigos de CatalogoAnexo13Dto.
    public string? CausaRuptura { get; set; }

    [StringLength(200, ErrorMessage = "El detalle de la causa no puede superar los 200 caracteres.")]
    public string? DetalleCausa { get; set; }

    public string? FormaRestablecimiento { get; set; }

    [StringLength(200, ErrorMessage = "El detalle no puede superar los 200 caracteres.")]
    public string? DetalleRestablecimiento { get; set; }

    public bool? VacunasInmovilizadas { get; set; }
}
