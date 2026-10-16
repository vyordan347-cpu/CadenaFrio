namespace CadenaFrio.Shared.Contracts;

// Alerta que exige revisión y todavía no ha sido revisada por el personal (RF08).
public class AlertaPendienteDto
{
    public int Id { get; set; }

    public TipoAlertaDto Tipo { get; set; }

    // Inicio del episodio y momento en que llegó a ruptura (UTC).
    public DateTime Inicio { get; set; }
    public DateTime? FechaRuptura { get; set; }

    // Cuándo volvió al rango óptimo (UTC). Nulo si sigue en curso.
    public DateTime? Fin { get; set; }

    // El episodio se conoció al sincronizar lecturas guardadas sin conexión (HU06).
    public bool DetectadaTarde { get; set; }

    // Temperatura más alejada del rango durante el episodio.
    public double ValorExtremo { get; set; }

    // Rango límite que se superó.
    public double LimiteMinimo { get; set; }
    public double LimiteMaximo { get; set; }
}
