namespace CadenaFrio.Shared.Contracts;

// Rango físico aceptado para una lectura. Descarta los valores que el sensor DS18B20
// entrega cuando falla: -127 °C (desconectado) y 85 °C (error al encenderse).
public static class LimitesFisicos
{
    public const double TemperaturaMinima = -50.0;
    public const double TemperaturaMaxima = 60.0;

    public static bool EsValida(double temperatura) =>
        temperatura >= TemperaturaMinima && temperatura <= TemperaturaMaxima;
}
