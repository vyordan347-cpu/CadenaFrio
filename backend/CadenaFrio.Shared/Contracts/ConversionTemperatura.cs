namespace CadenaFrio.Shared.Contracts;

// Conversión entre °C y °F para ingresar rangos copiados de un inserto en °F.
// El sistema guarda todo en °C (la NTS 242 y los formatos del MINSA usan °C):
// los °F solo sirven para escribir el valor; se convierten antes de guardar.
public static class ConversionTemperatura
{
    // Más cerca de un número entero que esto, probablemente el inserto redondeó.
    public const double ToleranciaRedondeo = 0.3;

    public static double ACelsius(double fahrenheit) => (fahrenheit - 32) * 5 / 9;

    public static double AFahrenheit(double celsius) => celsius * 9 / 5 + 32;

    // Los rangos se guardan con una décima: 46 °F → 7.8 °C.
    public static double RedondearDecima(double valor) => Math.Round(valor, 1, MidpointRounding.AwayFromZero);

    // Los insertos suelen redondear: "2 °C a 8 °C (36 °F a 46 °F)", pero 46 °F son 7.78 °C.
    // Si el valor en °C queda muy cerca de un entero sin serlo, devuelve ese entero como sugerencia.
    public static double? EnteroSugerido(double celsius)
    {
        var entero = Math.Round(celsius, MidpointRounding.AwayFromZero);
        var diferencia = Math.Abs(celsius - entero);
        return diferencia > 0.001 && diferencia <= ToleranciaRedondeo ? entero : null;
    }
}
