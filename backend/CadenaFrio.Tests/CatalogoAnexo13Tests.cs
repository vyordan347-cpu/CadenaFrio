using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;

namespace CadenaFrio.Tests;

// Notificación de ruptura con las listas del Anexo N° 13 (NTS 242).
public class CatalogoAnexo13Tests
{
    [Fact]
    public void Notificacion_completa_de_refrigeradora_es_valida()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Refrigeradora,
            "energia-apagon", null, "traslado-termo", null, true);

        Assert.Null(error);
    }

    [Fact]
    public void Causa_que_no_esta_en_la_lista_se_rechaza()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Refrigeradora,
            "se-fue-la-luz", null, "traslado-termo", null, false);

        Assert.NotNull(error);
    }

    [Fact]
    public void Causa_de_termo_no_se_acepta_en_una_refrigeradora()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Refrigeradora,
            "no-cambio-pf", null, "traslado-termo", null, false);

        Assert.NotNull(error);
    }

    [Fact]
    public void Causa_de_refrigeradora_no_se_acepta_en_un_termo()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Termo,
            "puerta-mal-cerrada", null, "traslado-otro-eess", null, false);

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("otra", "traslado-termo", null)]
    [InlineData("energia-otro", "traslado-termo", "  ")]
    public void Otra_causa_exige_especificar_cual(string causa, string forma, string? detalle)
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Refrigeradora,
            causa, detalle, forma, null, false);

        Assert.NotNull(error);
    }

    [Fact]
    public void Otra_causa_con_detalle_es_valida()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Termo,
            "otra", "Se dejó el termo al sol", "traslado-termo", null, false);

        Assert.Null(error);
    }

    [Fact]
    public void Otra_forma_de_restablecer_exige_especificar_cual()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Refrigeradora,
            "equipo-malogrado", null, "otros", null, false);

        Assert.NotNull(error);
    }

    [Fact]
    public void Debe_indicarse_si_las_vacunas_estan_inmovilizadas()
    {
        var error = CatalogoAnexo13.Validar(TipoEquipo.Refrigeradora,
            "equipo-malogrado", null, "traslado-termo", null, null);

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(TipoAlerta.TemperaturaAlta, NivelAlerta.Ruptura, true)]
    [InlineData(TipoAlerta.TemperaturaBaja, NivelAlerta.Ruptura, true)]
    [InlineData(TipoAlerta.TemperaturaAlta, NivelAlerta.Advertencia, false)]
    [InlineData(TipoAlerta.SinSenal, NivelAlerta.Ruptura, false)]
    [InlineData(TipoAlerta.SensorSinVariacion, NivelAlerta.Ruptura, false)]
    public void Anexo_13_solo_para_rupturas_de_temperatura(TipoAlerta tipo, NivelAlerta nivel, bool esperado)
    {
        Assert.Equal(esperado, CatalogoAnexo13.Aplica(tipo, nivel));
    }

    [Fact]
    public void Los_codigos_no_se_repiten_dentro_de_cada_lista()
    {
        var listas = new[]
        {
            CatalogoAnexo13.CausasRefrigerador,
            CatalogoAnexo13.CausasTermo,
            CatalogoAnexo13.FormasRestablecimiento
        };

        foreach (var lista in listas)
        {
            Assert.Equal(lista.Count, lista.Select(o => o.Codigo).Distinct().Count());
        }
    }
}
