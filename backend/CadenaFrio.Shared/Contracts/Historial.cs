namespace CadenaFrio.Shared.Contracts;

// Historial de un equipo en un periodo (HU07).
public class HistorialDto
{
    public int EquipoId { get; set; }
    public string NombreEquipo { get; set; } = string.Empty;

    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }

    // Rango que regía al final del periodo (el último tramo de Rangos). Si el periodo llega
    // hasta hoy, es el rango vigente del equipo.
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }

    // Rangos que rigieron en el periodo, en orden y sin huecos: uno solo si no cambió.
    // Cada lectura se juzga con el rango de su tramo.
    public List<TramoRangoDto> Rangos { get; set; } = new();

    // Lecturas resumidas por tramos: con periodos largos hay miles de lecturas,
    // y cada tramo conserva el mínimo y el máximo para no perder los picos.
    public List<PuntoHistorialDto> Puntos { get; set; } = new();

    public List<AlertaHistorialDto> Alertas { get; set; } = new();

    public EstadisticasHistorialDto Estadisticas { get; set; } = new();
}

// Un tramo [Desde, Hasta) en el que rigió un mismo rango (UTC).
public class TramoRangoDto
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public double OptimoMinimo { get; set; }
    public double OptimoMaximo { get; set; }
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }
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

    // Ruptura revisada con el formato del Anexo N° 13: se puede descargar el Excel.
    public bool TieneAnexo13 { get; set; }

    // Texto de la causa elegida, por ejemplo "Apagón". Nulo si no tiene Anexo 13.
    public string? CausaRuptura { get; set; }
}

public class EstadisticasHistorialDto
{
    public int CantidadLecturas { get; set; }
    public double? Minima { get; set; }
    public double? Maxima { get; set; }
    public double? Promedio { get; set; }

    // Porcentaje de lecturas dentro del rango óptimo: el indicador de calidad de la cadena de frío.
    // Cada lectura se compara con el rango que regía cuando se tomó.
    public double? PorcentajeOptimo { get; set; }

    // Lecturas fuera del rango límite (cada una con el rango de su momento).
    public int CantidadFueraDelLimite { get; set; }

    public int CantidadAlertas { get; set; }
    public int CantidadRupturas { get; set; }

    // Tiempo total en ruptura dentro del periodo.
    public double MinutosEnRuptura { get; set; }
}
