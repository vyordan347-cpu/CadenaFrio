using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;

namespace CadenaFrio.Tests;

// Clasificación de temperaturas según la NTS N° 242-MINSA/DGIESP-2026:
// ruptura fuera de +2 a +8 °C; alerta de +2 a +3 °C y de +7 a +8 °C.
public class ClasificacionTests
{
    private static readonly LimitesTemperatura Norma = LimitesTemperatura.PorDefecto;

    [Fact]
    public void Los_valores_por_defecto_son_los_de_la_NTS_242()
    {
        Assert.Equal(new LimitesTemperatura(3.0, 7.0, 2.0, 8.0), Norma);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(3.0)]   // borde inferior del óptimo: todavía normal
    [InlineData(7.0)]   // borde superior del óptimo: todavía normal
    public void Dentro_del_optimo_no_hay_alerta(double temperatura)
    {
        var (tipo, nivel) = Norma.Clasificar(temperatura);

        Assert.Null(tipo);
        Assert.Null(nivel);
    }

    [Theory]
    [InlineData(7.5, TipoAlerta.TemperaturaAlta)]
    [InlineData(8.0, TipoAlerta.TemperaturaAlta)]   // exactamente +8 °C: alerta, no ruptura
    [InlineData(2.5, TipoAlerta.TemperaturaBaja)]
    [InlineData(2.0, TipoAlerta.TemperaturaBaja)]   // exactamente +2 °C: alerta, no ruptura
    public void En_la_zona_de_alerta_es_advertencia(double temperatura, TipoAlerta esperado)
    {
        var (tipo, nivel) = Norma.Clasificar(temperatura);

        Assert.Equal(esperado, tipo);
        Assert.Equal(NivelAlerta.Advertencia, nivel);
    }

    [Theory]
    [InlineData(8.1, TipoAlerta.TemperaturaAlta)]
    [InlineData(25.0, TipoAlerta.TemperaturaAlta)]
    [InlineData(1.9, TipoAlerta.TemperaturaBaja)]   // bajo +2 °C ya es ruptura según la NTS 242
    [InlineData(-5.0, TipoAlerta.TemperaturaBaja)]
    public void Fuera_de_2_a_8_grados_es_ruptura(double temperatura, TipoAlerta esperado)
    {
        var (tipo, nivel) = Norma.Clasificar(temperatura);

        Assert.Equal(esperado, tipo);
        Assert.Equal(NivelAlerta.Ruptura, nivel);
    }
}
