using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Tests;

// Detector de silencio (HU04, HU05): equipos que dejaron de enviar datos.
public class DetectorSilencioTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly NotificadorDePrueba _notificador = new();
    private readonly DetectorSilencio _detector;

    public DetectorSilencioTests()
    {
        _detector = new DetectorSilencio(_base.Db, _notificador, BaseDeDatosDePrueba.Monitoreo(minutosSinSenal: 5, minutosParaRevision: 30));
    }

    // Simula que la última lectura llegó hace cierta cantidad de minutos.
    private async Task UltimaLecturaHaceAsync(int minutos)
    {
        var fecha = DateTime.UtcNow.AddMinutes(-minutos);
        _base.Db.Lecturas.Add(new Lectura { EquipoId = 1, Temperatura = 5.0, FechaMedicion = fecha, FechaRecepcion = fecha });
        await _base.Db.SaveChangesAsync();
    }

    private Task<Alerta?> AlertaSinSenalAsync() =>
        _base.Db.Alertas.AsNoTracking().FirstOrDefaultAsync(a => a.Tipo == TipoAlerta.SinSenal);

    [Fact]
    public async Task Con_datos_recientes_no_hay_alerta()
    {
        await UltimaLecturaHaceAsync(2);

        await _detector.RevisarAsync(CancellationToken.None);

        Assert.Null(await AlertaSinSenalAsync());
    }

    [Fact]
    public async Task Mas_de_5_minutos_sin_datos_abre_sin_senal_sin_exigir_revision()
    {
        await UltimaLecturaHaceAsync(10);

        await _detector.RevisarAsync(CancellationToken.None);

        var alerta = await AlertaSinSenalAsync();
        Assert.NotNull(alerta);
        Assert.Null(alerta.Fin);
        Assert.Null(alerta.RevisionRequeridaDesde);
        Assert.Contains("Abierta:SinSenal:Advertencia", _notificador.Eventos);
    }

    [Fact]
    public async Task Un_silencio_de_mas_de_30_minutos_exige_revision()
    {
        await UltimaLecturaHaceAsync(40);

        await _detector.RevisarAsync(CancellationToken.None);

        var alerta = await AlertaSinSenalAsync();
        Assert.NotNull(alerta);
        Assert.NotNull(alerta.RevisionRequeridaDesde);
    }

    [Fact]
    public async Task Un_equipo_que_nunca_envio_datos_no_genera_alerta()
    {
        await _detector.RevisarAsync(CancellationToken.None);

        Assert.Null(await AlertaSinSenalAsync());
    }

    public void Dispose() => _base.Dispose();
}
