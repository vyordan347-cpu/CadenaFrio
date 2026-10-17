using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CadenaFrio.Shared.Contracts;

// Tipo de equipo tal como lo ve la web. Se envía como texto ("Refrigeradora").
[JsonConverter(typeof(JsonStringEnumConverter<TipoEquipoDto>))]
public enum TipoEquipoDto
{
    Refrigeradora = 1,
    Termo = 2
}

// Un equipo en la lista del panel de administrador (RF19).
public class EquipoAdminDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoEquipoDto Tipo { get; set; }
    public string Ubicacion { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }

    // Última vez que el servidor recibió datos del equipo (UTC). Nulo si nunca envió.
    public DateTime? UltimaSenal { get; set; }
}

// Datos para registrar o editar un equipo.
public class GuardarEquipoRequest
{
    [Required(ErrorMessage = "Ingrese un nombre.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "Elija el tipo de equipo.")]
    public TipoEquipoDto? Tipo { get; set; }

    [StringLength(150, ErrorMessage = "La ubicación no puede superar 150 caracteres.")]
    public string? Ubicacion { get; set; }
}

// Clave recién generada. Solo se envía una vez: en la base queda únicamente su huella.
public class ClaveGeneradaDto
{
    public EquipoAdminDto Equipo { get; set; } = new();
    public string Clave { get; set; } = string.Empty;
}
