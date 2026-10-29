namespace CadenaFrio.Shared.Contracts;

// Historial de un equipo en un periodo (HU07).
public class HistorialDto
{
    public int EquipoId { get; set; }
    public string NombreEquipo { get; set; } = string.Empty;

    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }

    // Rangos vigentes del equipo, para dibujar las zonas del gráfico.
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }

    // Lecturas resumidas por tramos: con periodos largos hay miles de lecturas,
    // y cada tramo conserva el mínimo y el máximo para no perder los picos.
    public List<PuntoHistorialDto> Puntos { get; set; } = new();

    public List<AlertaHistorialDto> Alertas { get; set; } = new();

    public EstadisticasHistorialDto Estadisticas { get; set; } = new();
}

public class PuntoHistorialDto
{
    // Inicio del tramo (UTC).
    public DateTime Fecha { get; set; }
    public double Promedio { get; set; }
    public double Minimo { get; set; }
    public double Maximo { get; set; }
    public int Cantidad { get; set; }
}

public class AlertaHistorialDto
{
    public int Id { get; set; }
    public TipoAlertaDto Tipo { get; set; }
    public NivelAlertaDto Nivel { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? FechaRuptura { get; set; }
    public DateTime? Fin { get; set; }
    public double ValorExtremo { get; set; }
    public bool DetectadaTarde { get; set; }

    // Revisión del personal (RF08).
    public bool RequiereRevision { get; set; }
    public string? AtendidaPor { get; set; }
    public DateTime? FechaAtencion { get; set; }
    public string? ObservacionAtencion { get; set; }
}

public class EstadisticasHistorialDto
{
    public int CantidadLecturas { get; set; }
    public double? Minima { get; set; }
    public double? Maxima { get; set; }
    public double? Promedio { get; set; }

    // Porcentaje de lecturas dentro del rango óptimo: el indicador de calidad de la cadena de frío.
    public double? PorcentajeOptimo { get; set; }

    public int CantidadAlertas { get; set; }
    public int CantidadRupturas { get; set; }

    // Tiempo total en ruptura dentro del periodo.
    public double MinutosEnRuptura { get; set; }
}
