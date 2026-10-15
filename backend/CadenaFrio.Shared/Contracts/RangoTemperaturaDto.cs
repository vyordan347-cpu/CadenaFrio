namespace CadenaFrio.Shared.Contracts;

// Rangos de temperatura de un equipo, tal como los ve la web (HU02).
public class RangoTemperaturaDto
{
    public int EquipoId { get; set; }

    // Rango óptimo: funcionamiento normal.
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }

    // Rango límite: fuera de él hay ruptura de la cadena de frío.
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }

    // Nulos cuando son los rangos por defecto (nadie los configuró).
    public DateTime? FechaCambio { get; set; }
    public string? ModificadoPor { get; set; }

    // Indica si son los rangos automáticos (óptimo 2–7 °C, límite 0–8 °C).
    public bool EsPorDefecto { get; set; }
}
