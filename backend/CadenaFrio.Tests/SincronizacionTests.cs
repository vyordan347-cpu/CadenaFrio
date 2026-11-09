using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Tests;

// Sincronización de lecturas guardadas sin conexión (HU06).
public class SincronizacionTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly RegistroLecturas _registro;
    private readonly DateTime _ahora = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    public SincronizacionTests()
    {
        var motor = new MotorAlertas(_base.Db, new NotificadorDePrueba(), BaseDeDatosDePrueba.Monitoreo());
        _registro = new RegistroLecturas(_base.Db, motor);
    }

    private LecturaEntrante Hace(int minutos, double temperatura) => new(temperatura, _ahora.AddMinutes(-minutos));

    [Fact]
    public async Task Un_lote_atrasado_reconstruye_la_ruptura_sin_cambiar_el_estado_actual()
    {
        // El equipo está bien ahora mismo.
        await _registro.RegistrarAsync(1, new[] { new LecturaEntrante(5.0, _ahora) }, _ahora);

        // Llegan lecturas de hace 2 horas: hubo una ruptura mientras no había conexión.
        await _registro.RegistrarAsync(1, new[]
        {
            Hace(120, 5.0), Hace(110, 6.5), Hace(100, 7.5), Hace(90, 9.0), Hace(80, 9.4), Hace(70, 6.0), Hace(60, 5.0)
        }, _ahora);

        var alerta = Assert.Single(await _base.Db.Alertas.AsNoTracking().ToListAsync());
        Assert.True(alerta.DetectadaTarde);
        Assert.Equal(NivelAlerta.Ruptura, alerta.Nivel);
        Assert.Equal(9.4, alerta.ValorExtremo);
        Assert.Equal(_ahora.AddMinutes(-90), alerta.FechaRuptura);
        Assert.Equal(_ahora.AddMinutes(-70), alerta.Fin);              // ya terminó: no altera el estado actual
        Assert.NotNull(alerta.RevisionRequeridaDesde);                  // pero exige revisión: nadie la vio
    }

    [Fact]
    public async Task Un_lote_reenviado_no_duplica_lecturas()
    {
        var lote = new[] { Hace(3, 4.0), Hace(2, 4.2), Hace(1, 4.1) };

        var primero = await _registro.RegistrarAsync(1, lote, _ahora);
        var segundo = await _registro.RegistrarAsync(1, lote, _ahora);

        Assert.Equal(3, primero.Guardadas.Count);
        Assert.Empty(segundo.Guardadas);
        Assert.Equal(3, segundo.Duplicadas);
        Assert.Equal(3, await _base.Db.Lecturas.CountAsync());
    }

    // Plazo del Anexo 13: 24 h desde que se DETECTA la ruptura (NTS 242, 6.10.1 y Anexo 13).

    [Fact]
    public async Task Una_ruptura_en_vivo_se_detecta_a_la_hora_de_la_lectura()
    {
        await _registro.RegistrarAsync(1, new[] { new LecturaEntrante(9.5, _ahora) }, _ahora);

        var alerta = Assert.Single(await _base.Db.Alertas.AsNoTracking().ToListAsync());
        var deteccion = await _base.Db.ObtenerDeteccionRupturaAsync(alerta.EquipoId, alerta.FechaRuptura);

        Assert.Equal(_ahora, alerta.FechaRuptura);
        Assert.Equal(_ahora, deteccion);
    }

    [Fact]
    public async Task Una_ruptura_que_llega_tarde_se_detecta_al_reconectarse()
    {
        // La ruptura ocurrió hace 30 h, pero las lecturas llegaron recién ahora (al reconectarse).
        await _registro.RegistrarAsync(1, new[] { Hace(30 * 60 + 10, 5.0), Hace(30 * 60, 9.5), Hace(29 * 60, 5.0) }, _ahora);

        var alerta = Assert.Single(await _base.Db.Alertas.AsNoTracking().ToListAsync());
        var deteccion = await _base.Db.ObtenerDeteccionRupturaAsync(alerta.EquipoId, alerta.FechaRuptura);

        Assert.True(alerta.DetectadaTarde);
        Assert.Equal(_ahora.AddHours(-30), alerta.FechaRuptura);
        Assert.Equal(_ahora, deteccion);
        // Con el criterio de antes (desde la ruptura) el plazo ya estaría vencido; desde la detección, no.
        Assert.True(alerta.FechaRuptura + CatalogoAnexo13.PlazoNotificacion < _ahora);
        Assert.True(deteccion + CatalogoAnexo13.PlazoNotificacion > _ahora);
    }

    [Fact]
    public async Task Sin_ruptura_no_hay_hora_de_deteccion()
    {
        Assert.Null(await _base.Db.ObtenerDeteccionRupturaAsync(1, null));
    }

    public void Dispose() => _base.Dispose();
}
