using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Tests;

// Motor de alertas (HU03): abrir, escalar, mantener el nivel y cerrar.
// Con los rangos por defecto de la NTS 242: óptimo 3–7 °C, límite 2–8 °C.
public class MotorAlertasTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly NotificadorDePrueba _notificador = new();
    private readonly RegistroLecturas _registro;
    private readonly DateTime _inicio = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    public MotorAlertasTests()
    {
        var motor = new MotorAlertas(_base.Db, _notificador, BaseDeDatosDePrueba.Monitoreo());
        _registro = new RegistroLecturas(_base.Db, motor);
    }

    // Envía una lectura "en vivo", medida y recibida en el mismo minuto.
    private Task EnviarAsync(double temperatura, int minuto)
    {
        var fecha = _inicio.AddMinutes(minuto);
        return _registro.RegistrarAsync(BaseDeDatosDePrueba.EquipoId,
            new[] { new LecturaEntrante(temperatura, fecha) }, ahora: fecha);
    }

    private Task<List<Alerta>> AlertasAsync() =>
        _base.Db.Alertas.AsNoTracking().OrderBy(a => a.Id).ToListAsync();

    [Fact]
    public async Task Una_temperatura_normal_no_abre_alertas()
    {
        await EnviarAsync(5.0, 0);

        Assert.Empty(await AlertasAsync());
        Assert.Empty(_notificador.Eventos);
    }

    [Fact]
    public async Task Salir_del_optimo_abre_una_advertencia_que_no_exige_revision()
    {
        await EnviarAsync(5.0, 0);
        await EnviarAsync(7.5, 1);

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(TipoAlerta.TemperaturaAlta, alerta.Tipo);
        Assert.Equal(NivelAlerta.Advertencia, alerta.Nivel);
        Assert.Null(alerta.Fin);
        Assert.Null(alerta.RevisionRequeridaDesde);
    }

    [Fact]
    public async Task La_advertencia_escala_a_ruptura_y_exige_revision()
    {
        await EnviarAsync(7.5, 0);
        await EnviarAsync(9.0, 1);

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(NivelAlerta.Ruptura, alerta.Nivel);
        Assert.Equal(_inicio.AddMinutes(1), alerta.FechaRuptura);
        Assert.Equal(_inicio.AddMinutes(1), alerta.RevisionRequeridaDesde);
        Assert.Contains("Escalada:TemperaturaAlta", _notificador.Eventos);
    }

    [Fact]
    public async Task Una_ruptura_nunca_baja_de_nivel_aunque_la_temperatura_mejore()
    {
        await EnviarAsync(9.0, 0);
        await EnviarAsync(7.5, 1);   // vuelve a la zona de alerta, pero las vacunas ya estuvieron expuestas

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(NivelAlerta.Ruptura, alerta.Nivel);
        Assert.Null(alerta.Fin);
    }

    [Fact]
    public async Task La_alerta_se_cierra_solo_al_volver_al_rango_optimo()
    {
        await EnviarAsync(9.0, 0);
        await EnviarAsync(7.5, 1);
        await EnviarAsync(5.0, 2);

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(_inicio.AddMinutes(2), alerta.Fin);
        Assert.Equal(9.0, alerta.ValorExtremo);
        Assert.Contains("Cerrada:TemperaturaAlta", _notificador.Eventos);
    }

    [Fact]
    public async Task Un_salto_directo_a_ruptura_abre_la_alerta_como_ruptura()
    {
        await EnviarAsync(5.0, 0);
        await EnviarAsync(9.5, 1);

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(NivelAlerta.Ruptura, alerta.Nivel);
        Assert.Equal(alerta.Inicio, alerta.FechaRuptura);
        Assert.Contains("Abierta:TemperaturaAlta:Ruptura", _notificador.Eventos);
    }

    [Fact]
    public async Task Bajo_2_grados_es_ruptura_por_temperatura_baja()
    {
        await EnviarAsync(1.5, 0);

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(TipoAlerta.TemperaturaBaja, alerta.Tipo);
        Assert.Equal(NivelAlerta.Ruptura, alerta.Nivel);
    }

    [Fact]
    public async Task El_valor_extremo_guarda_el_pico_mas_alto()
    {
        await EnviarAsync(9.0, 0);
        await EnviarAsync(11.3, 1);
        await EnviarAsync(10.0, 2);

        var alerta = Assert.Single(await AlertasAsync());
        Assert.Equal(11.3, alerta.ValorExtremo);
    }

    public void Dispose() => _base.Dispose();
}
