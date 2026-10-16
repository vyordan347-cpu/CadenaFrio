using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Entities;

// Episodio que requiere atención del personal (HU03, RF06, RF08).
// Se abre al salir del rango óptimo, puede escalar a ruptura y se cierra
// cuando la temperatura vuelve al rango óptimo.
public class Alerta
{
    public int Id { get; set; }

    public int EquipoId { get; set; }
    public Equipo Equipo { get; set; } = null!;

    public TipoAlerta Tipo { get; set; }

    // Nivel más grave alcanzado durante el episodio. Nunca baja: si hubo ruptura,
    // las vacunas ya estuvieron expuestas aunque la temperatura mejore después.
    public NivelAlerta Nivel { get; set; }

    // Hora de medición de la primera lectura fuera del rango óptimo (UTC).
    public DateTime Inicio { get; set; }

    // Hora en que el episodio llegó a ruptura (UTC). Nulo si nunca la alcanzó.
    // La diferencia con Inicio es el margen que tuvo el personal para actuar.
    public DateTime? FechaRuptura { get; set; }

    // Hora en que la temperatura volvió al rango óptimo (UTC). Nulo mientras siga activa.
    public DateTime? Fin { get; set; }

    // Indica que el episodio ocurrió mientras no había conexión y se conoció después,
    // al sincronizar las lecturas guardadas en el dispositivo (HU06).
    public bool DetectadaTarde { get; set; }

    // La temperatura más alejada del rango durante el episodio.
    public double ValorExtremo { get; set; }

    // Rangos vigentes cuando se abrió la alerta. Se guardan porque pueden cambiar
    // después, y la alerta debe seguir explicando por qué se abrió.
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }

    // En la base de datos siguen llamándose RangoMinimo y RangoMaximo (ver el DbContext).
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }

    // Desde cuándo la alerta exige revisión del personal (UTC). Nulo si no la exige.
    // - Temperatura: desde que llegó a ruptura.
    // - Sin señal: desde que el silencio superó el tiempo permitido (por ejemplo, 30 min).
    public DateTime? RevisionRequeridaDesde { get; set; }

    // Revisión del personal (RF08). Que la alerta termine (Fin) es un hecho físico;
    // que esté atendida es una responsabilidad humana: alguien la revisó y registró qué hizo.
    // Queda pendiente si exige revisión y nunca se atendió, o si se atendió antes de que
    // la exigiera (por ejemplo, se atendió la advertencia y luego escaló a ruptura).
    public string? AtendidaPor { get; set; }
    public DateTime? FechaAtencion { get; set; }
    public string? ObservacionAtencion { get; set; }
}
