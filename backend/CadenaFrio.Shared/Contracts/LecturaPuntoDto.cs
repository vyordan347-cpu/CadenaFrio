namespace CadenaFrio.Shared.Contracts;

// Una lectura reducida a lo esencial, para el mini gráfico y la tendencia del indicador.
public class LecturaPuntoDto
{
    public DateTime FechaMedicion { get; set; }
    public double Temperatura { get; set; }
}
