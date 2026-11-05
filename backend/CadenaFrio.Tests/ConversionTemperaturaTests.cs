using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Tests;

// Ingreso de rangos en °F: el sistema siempre guarda en °C.
public class ConversionTemperaturaTests
{
    [Theory]
    [InlineData(32, 0)]
    [InlineData(212, 100)]
    [InlineData(-40, -40)]
    [InlineData(35.6, 2)]
    [InlineData(46.4, 8)]
    public void Convierte_fahrenheit_a_celsius(double fahrenheit, double celsius)
    {
        Assert.Equal(celsius, ConversionTemperatura.ACelsius(fahrenheit), precision: 6);
        Assert.Equal(fahrenheit, ConversionTemperatura.AFahrenheit(celsius), precision: 6);
    }

    [Fact]
    public void Los_valores_redondeados_del_inserto_no_dan_exactamente_2_y_8()
    {
        // "2 °C a 8 °C (36 °F a 46 °F)": convertidos tal cual, quedan un poco adentro.
        Assert.Equal(2.2, ConversionTemperatura.RedondearDecima(ConversionTemperatura.ACelsius(36)));
        Assert.Equal(7.8, ConversionTemperatura.RedondearDecima(ConversionTemperatura.ACelsius(46)));
    }

    [Theory]
    [InlineData(7.78, 8.0)]    // 46 °F
    [InlineData(2.22, 2.0)]    // 36 °F
    [InlineData(-14.8, -15.0)] // 5 °F
    public void Sugiere_el_entero_cuando_el_inserto_parece_haber_redondeado(double celsius, double sugerido)
    {
        Assert.Equal(sugerido, ConversionTemperatura.EnteroSugerido(celsius));
    }

    [Theory]
    [InlineData(8.0)]   // ya es entero
    [InlineData(7.5)]   // demasiado lejos de un entero: es un valor real, no un redondeo
    [InlineData(3.6)]
    public void No_sugiere_nada_si_no_hace_falta(double celsius)
    {
        Assert.Null(ConversionTemperatura.EnteroSugerido(celsius));
    }

    [Fact]
    public void Redondea_a_una_decima()
    {
        Assert.Equal(7.8, ConversionTemperatura.RedondearDecima(7.7777));
        Assert.Equal(2.3, ConversionTemperatura.RedondearDecima(2.25));
    }
}
