namespace CadenaFrio.Domain.Entities;

// Cada medición de temperatura enviada por un dispositivo.
// Una vez registrada no se modifica ni se elimina (RNF07).
public class Lectura
{
    // long porque con una lectura por minuto se acumulan
    // más de 500 000 registros al año por equipo.
    public long Id { get; set; }

    // Clave foránea: a qué equipo pertenece la lectura.
    public int EquipoId { get; set; }

    // Propiedad de navegación hacia el equipo.
    public Equipo Equipo { get; set; } = null!;

    // Temperatura en °C.
    public double Temperatura { get; set; }

    // Cuándo midió el dispositivo (UTC). En una sincronización tras
    // perder conexión (HU06) puede ser varias horas anterior a la recepción.
    public DateTime FechaMedicion { get; set; }

    // Cuándo llegó la lectura al servidor (UTC).
    public DateTime FechaRecepcion { get; set; } = DateTime.UtcNow;
}
