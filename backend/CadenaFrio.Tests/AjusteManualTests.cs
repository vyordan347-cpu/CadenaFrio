using System.ComponentModel.DataAnnotations;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Tests;

// Ajuste manual del rango (Rangos de conservación → Ajuste avanzado): puede ser más estricto que
// el rango de referencia, nunca más amplio, y exige un motivo.
public class AjusteManualTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly GestionRangos _rangos;

    // Vacuna 1 de los datos semilla: Esquema regular (PNI), óptimo 3–7 °C, límite 2–8 °C.
    private const int VacunaPni = 1;

    public AjusteManualTests()
    {
        _rangos = new GestionRangos(_base.Db, new Bitacora(_base.Db));
    }

    [Fact]
    public void Un_rango_mas_estricto_esta_dentro_de_la_referencia()
    {
        Assert.True(new LimitesTemperatura(4, 6, 3, 7).DentroDe(LimitesTemperatura.PorDefecto));
        Assert.True(LimitesTemperatura.PorDefecto.DentroDe(LimitesTemperatura.PorDefecto)); // igual también vale
    }

    [Theory]
    [InlineData(2.5, 7, 2, 8)]    // óptimo mínimo más bajo
    [InlineData(3, 7.5, 2, 8)]    // óptimo máximo más alto
    [InlineData(3, 7, 1, 8)]      // límite mínimo más bajo
    [InlineData(3, 7, 2, 9)]      // límite máximo más alto
    public void Un_rango_mas_amplio_no_esta_dentro(double optMin, double optMax, double limMin, double limMax)
    {
        Assert.False(new LimitesTemperatura(optMin, optMax, limMin, limMax).DentroDe(LimitesTemperatura.PorDefecto));
    }

    [Fact]
    public async Task Sin_vacunas_la_referencia_es_la_norma()
    {
        var (referencia, origen) = await _rangos.ReferenciaAjusteAsync(BaseDeDatosDePrueba.EquipoId);

        Assert.Equal(LimitesTemperatura.PorDefecto, referencia);
        Assert.Equal("la NTS 242", origen);
    }

    [Fact]
    public async Task Con_vacunas_la_referencia_es_su_rango_comun()
    {
        _base.Db.Vacunas.Add(new Vacuna { Id = 50, Nombre = "Vacuna estricta", OptimoMinimo = 4, OptimoMaximo = 6, LimiteMinimo = 3, LimiteMaximo = 7, Activo = true, Fuente = "Prueba" });
        await _base.Db.SaveChangesAsync();
        await _rangos.AsignarVacunasAsync(BaseDeDatosDePrueba.EquipoId, new[] { VacunaPni, 50 }, "Prueba");

        var (referencia, origen) = await _rangos.ReferenciaAjusteAsync(BaseDeDatosDePrueba.EquipoId);

        Assert.Equal(new LimitesTemperatura(4, 6, 3, 7), referencia);
        Assert.Contains("Vacuna estricta", origen);
    }

    [Fact]
    public void Un_ajuste_mas_amplio_se_rechaza_y_dice_el_rango_permitido()
    {
        var error = GestionRangos.ValidarAjusteManual(new LimitesTemperatura(2, 9, 1, 10), LimitesTemperatura.PorDefecto, "la NTS 242");

        Assert.NotNull(error);
        Assert.Contains("la NTS 242", error);
        Assert.Contains("nunca más amplio", error);
        Assert.Null(GestionRangos.ValidarAjusteManual(new LimitesTemperatura(4, 6, 3, 7), LimitesTemperatura.PorDefecto, "la NTS 242"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("corto", false)]
    [InlineData("Inserto de la vacuna nueva más estricto", true)]
    public void El_motivo_es_obligatorio_y_de_al_menos_10_caracteres(string? motivo, bool valido)
    {
        var solicitud = new ConfigurarRangoRequest { OptimoMinimo = 4, OptimoMaximo = 6, LimiteMinimo = 3, LimiteMaximo = 7, Motivo = motivo };
        var errores = new List<ValidationResult>();

        Validator.TryValidateObject(solicitud, new ValidationContext(solicitud), errores, validateAllProperties: true);

        Assert.Equal(valido, errores.All(e => !e.MemberNames.Contains(nameof(ConfigurarRangoRequest.Motivo))));
    }

    public void Dispose() => _base.Dispose();
}
