using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Entities;

// Representa un equipo monitoreado: una refrigeradora o un termo portavacunas.
public class Equipo
{
    public int Id { get; set; }

    // Nombre visible para el personal, por ejemplo "Refrigeradora principal".
    public string Nombre { get; set; } = string.Empty;

    public TipoEquipo Tipo { get; set; }

    // Dónde se encuentra dentro de la posta, por ejemplo "Sala de inmunizaciones".
    public string Ubicacion { get; set; } = string.Empty;

    // Clave secreta con la que el ESP32 se identifica al enviar lecturas (RNF06).
    public string ClaveDispositivo { get; set; } = string.Empty;

    // Permite dar de baja un equipo sin borrar su historial.
    public bool Activo { get; set; } = true;

    // Se guarda en UTC; se convierte a hora de Perú solo al mostrarla.
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Propiedad de navegación: todas las lecturas de este equipo.
    public ICollection<Lectura> Lecturas { get; set; } = new List<Lectura>();
}
