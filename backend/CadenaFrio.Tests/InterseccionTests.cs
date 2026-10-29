using CadenaFrio.Domain.Valores;

namespace CadenaFrio.Tests;

// Rango común de las vacunas que guarda un equipo.
public class InterseccionTests
{
    [Fact]
    public void Una_sola_vacuna_conserva_su_rango()
    {
        var rango = new LimitesTemperatura(3, 7, 2, 8);

        Assert.Equal(rango, LimitesTemperatura.Interseccion(new[] { rango }));
    }

    [Fact]
    public void Dos_vacunas_compatibles_dan_el_rango_mas_estricto()
    {
        var regular = new LimitesTemperatura(3, 7, 2, 8);
        var otra = new LimitesTemperatura(4, 6, 3, 7);

        var comun = LimitesTemperatura.Interseccion(new[] { regular, otra });

        Assert.Equal(new LimitesTemperatura(4, 6, 3, 7), comun);
    }

    [Fact]
    public void Vacunas_incompatibles_no_tienen_rango_comun()
    {
        var regular = new LimitesTemperatura(3, 7, 2, 8);
        var vacunaX = new LimitesTemperatura(10, 15, 9, 16);

        Assert.Null(LimitesTemperatura.Interseccion(new[] { regular, vacunaX }));
    }

    [Fact]
    public void Sin_vacunas_no_hay_rango()
    {
        Assert.Null(LimitesTemperatura.Interseccion(Array.Empty<LimitesTemperatura>()));
    }
}
