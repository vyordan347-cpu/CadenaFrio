namespace CadenaFrio.Shared.Contracts;

// Indicadores clave del sistema en un periodo (sección 8 del documento: métricas).
public class IndicadoresDto
{
    public int Dias { get; set; }

    // Porcentaje de lecturas dentro del rango óptimo de su equipo.
    public double? PorcentajeOptimo { get; set; }

    // Rupturas de temperatura que empezaron en el periodo.
    public int Rupturas { get; set; }

    // Tiempo promedio entre que una alerta exige revisión y alguien la atiende.
    public double? MinutosPromedioRespuesta { get; set; }

    // Lecturas recibidas frente a las esperadas (una por minuto por equipo activo).
    public double? PorcentajeDisponibilidad { get; set; }

    // Alertas que siguen esperando revisión.
    public int PendientesDeRevision { get; set; }
}
