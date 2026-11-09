using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Notificaciones;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Tests;

// Resumen diario: junta las cifras de un día de Perú (UTC-5) y calcula cuándo enviarlo.
public class ResumenDiarioTests : IDisposable
{
    // 8 de noviembre de 2026 en Perú: de las 05:00 UTC del 8 a las 05:00 UTC del 9.
    private static readonly DateOnly Dia = new(2026, 11, 8);
    private static readonly DateTime InicioDia = new(2026, 11, 8, 5, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Ahora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc); // 07:00 de Perú

    private readonly BaseDeDatosDePrueba _base = new();
    private readonly ResumenDiario _resumen;

    public ResumenDiarioTests()
    {
        // El cliente de Telegram queda sin token: estas pruebas no envían nada.
        var telegram = new ClienteTelegram(new HttpClient(), Options.Create(new OpcionesNotificaciones()));
        _resumen = new ResumenDiario(_base.Db, new CalculoIndicadores(_base.Db), telegram,
            Options.Create(new OpcionesEstablecimiento { Nombre = "Posta X" }));
    }

    private void Lecturas(DateTime inicio, int cantidad, double temperatura)
    {
        for (var i = 0; i < cantidad; i++)
        {
            var hora = inicio.AddMinutes(i);
            _base.Db.Lecturas.Add(new Lectura
            {
                EquipoId = BaseDeDatosDePrueba.EquipoId, Temperatura = temperatura,
                FechaMedicion = hora, FechaRecepcion = hora
            });
        }
    }

    [Fact]
    public async Task Cuenta_solo_las_lecturas_del_dia_de_Peru()
    {
        Lecturas(InicioDia.AddMinutes(-10), 10, 9);   // 23:50 del día 7 en Perú: no cuenta
        Lecturas(InicioDia, 60, 5);
        Lecturas(InicioDia.AddHours(23), 60, 4);
        await _base.Db.SaveChangesAsync();

        var texto = await _resumen.PrepararAsync(Dia, Ahora);

        Assert.Contains("Resumen del 08/11/2026</b> — Posta X", texto);
        Assert.Contains("Mín 4 °C · Máx 5 °C", texto);
        Assert.Contains("Lecturas: 120 de 1 440 esperadas", texto);
        Assert.Contains("100 % de las lecturas en rango óptimo", texto);
    }

    [Fact]
    public async Task Incluye_las_rupturas_pendientes_con_su_plazo()
    {
        var ruptura = InicioDia.AddHours(14); // 14:00 de Perú
        Lecturas(ruptura, 1, 9);
        _base.Db.Alertas.Add(new Alerta
        {
            EquipoId = BaseDeDatosDePrueba.EquipoId, Tipo = TipoAlerta.TemperaturaAlta, Nivel = NivelAlerta.Ruptura,
            Inicio = ruptura, FechaRuptura = ruptura, RevisionRequeridaDesde = ruptura, Fin = ruptura.AddMinutes(20),
            ValorExtremo = 9, OptimoMinimo = 3, OptimoMaximo = 7, LimiteMinimo = 2, LimiteMaximo = 8
        });
        await _base.Db.SaveChangesAsync();

        var texto = await _resumen.PrepararAsync(Dia, Ahora);

        Assert.Contains("1 ruptura de temperatura", texto);
        Assert.Contains("Revisiones pendientes: 1", texto);
        Assert.Contains("ruptura: notificar antes del 09/11 14:00", texto);
    }

    [Fact]
    public async Task Sin_lecturas_lo_dice()
    {
        var texto = await _resumen.PrepararAsync(Dia, Ahora);

        Assert.Contains("Sin lecturas en todo el día", texto);
    }

    [Fact]
    public void El_envio_es_a_la_hora_de_Peru()
    {
        // A las 06:00 de Perú del día 9, el próximo envío es a las 07:00 de ese día (12:00 UTC).
        Assert.Equal(new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc),
            ResumenDiario.ProximoEnvio(new DateTime(2026, 11, 9, 11, 0, 0, DateTimeKind.Utc), 7));

        // Pasadas las 07:00, el siguiente es al día siguiente.
        Assert.Equal(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            ResumenDiario.ProximoEnvio(new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc), 7));

        // A las 07:00 de Perú corresponde el resumen del día anterior.
        Assert.Equal(new DateOnly(2026, 11, 8), ResumenDiario.DiaPeru(Ahora).AddDays(-1));
    }

    public void Dispose() => _base.Dispose();
}
