namespace CadenaFrio.Shared.Contracts;

// Resumen de lo que pasó con un lote, para que el dispositivo sepa qué se guardó.
public class ResultadoLoteDto
{
    public int Recibidas { get; set; }
    public int Guardadas { get; set; }

    // Lecturas que ya estaban registradas (por ejemplo, si el lote se reenvió).
    public int Duplicadas { get; set; }

    // Lecturas descartadas, con el motivo de cada una.
    public List<string> Descartadas { get; set; } = new();
}
