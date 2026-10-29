using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Notificaciones;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Tests;

// Escalamiento: si nadie confirma "Lo estoy atendiendo" a tiempo, se avisa a otra persona.
public class EscalamientoTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly EscaladorAlertas _escalador;

    public EscalamientoTests()
    {
        // Sin token ni correo configurados: el escalador decide, pero no envía mensajes reales.
        var opciones = Options.Create(new OpcionesNotificaciones { MinutosParaEscalar = 15 });
        var telegram = new ClienteTelegram(new HttpClient(), opciones);
        var correo = new NotificadorCorreo(_base.Db, opciones);
        _escalador = new EscaladorAlertas(_base.Db, telegram, correo, opciones, NullLogger<EscaladorAlertas>.Instance);
    }

    private async Task<int> RupturaHaceAsync(int minutos, string? reconocidaPor = null)
    {
        var inicio = DateTime.UtcNow.AddMinutes(-minutos);
        var alerta = new Alerta
        {
            EquipoId = 1,
            Tipo = TipoAlerta.TemperaturaAlta,
            Nivel = NivelAlerta.Ruptura,
            Inicio = inicio,
            FechaRuptura = inicio,
            RevisionRequeridaDesde = inicio,
            ValorExtremo = 9.5,
            OptimoMinimo = 3, OptimoMaximo = 7, LimiteMinimo = 2, LimiteMaximo = 8,
            ReconocidaPor = reconocidaPor,
            FechaReconocimiento = reconocidaPor is null ? null : inicio.AddMinutes(1)
        };
        _base.Db.Alertas.Add(alerta);
        await _base.Db.SaveChangesAsync();
        _base.Db.ChangeTracker.Clear();
        return alerta.Id;
    }

    private Task<Alerta> AlertaAsync(int id) => _base.Db.Alertas.AsNoTracking().FirstAsync(a => a.Id == id);

    [Fact]
    public async Task Una_ruptura_sin_atender_por_mas_de_15_minutos_se_escala()
    {
        var id = await RupturaHaceAsync(20);

        await _escalador.RevisarAsync(CancellationToken.None);

        Assert.NotNull((await AlertaAsync(id)).FechaEscalamiento);
    }

    [Fact]
    public async Task Una_ruptura_atendida_no_se_escala()
    {
        var id = await RupturaHaceAsync(20, reconocidaPor: "Rosa Quispe");

        await _escalador.RevisarAsync(CancellationToken.None);

        Assert.Null((await AlertaAsync(id)).FechaEscalamiento);
    }

    [Fact]
    public async Task Una_ruptura_reciente_todavia_no_se_escala()
    {
        var id = await RupturaHaceAsync(5);

        await _escalador.RevisarAsync(CancellationToken.None);

        Assert.Null((await AlertaAsync(id)).FechaEscalamiento);
    }

    public void Dispose() => _base.Dispose();
}
