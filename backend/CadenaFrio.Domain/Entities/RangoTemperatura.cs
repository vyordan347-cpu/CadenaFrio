namespace CadenaFrio.Domain.Entities;

// Rangos de temperatura de un equipo (HU02).
// Cada cambio crea una fila nueva: el vigente es el más reciente
// y los anteriores quedan como historial de cambios (RF05).
public class RangoTemperatura
{
    public int Id { get; set; }

    public int EquipoId { get; set; }
    public Equipo Equipo { get; set; } = null!;

    // Rango óptimo: funcionamiento normal. Fuera de él se abre una advertencia.
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }

    // Rango límite: fuera de él hay ruptura de la cadena de frío.
    // En la base de datos siguen llamándose TemperaturaMinima y TemperaturaMaxima
    // (ver el DbContext), así no se pierden los rangos registrados antes.
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }

    // Cuándo se definió este rango (UTC).
    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;

    // Nombre de quien hizo el cambio, tomado de la sesión iniciada.
    public string ModificadoPor { get; set; } = string.Empty;
}
