namespace CadenaFrio.Domain.Entities;

// Vacuna del catálogo, con los rangos de temperatura en que debe conservarse.
// Define el rango de los equipos que la guardan (no es inventario: no hay cantidades ni lotes).
public class Vacuna
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    // Rango óptimo y rango límite, igual que en RangoTemperatura.
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }

    // De dónde sale el dato, por ejemplo "NTS N° 242-MINSA/DGIESP-2026".
    public string Fuente { get; set; } = string.Empty;

    // Una vacuna retirada del catálogo no se borra: se desactiva.
    public bool Activo { get; set; } = true;

    public ICollection<EquipoVacuna> Equipos { get; set; } = new List<EquipoVacuna>();
}
