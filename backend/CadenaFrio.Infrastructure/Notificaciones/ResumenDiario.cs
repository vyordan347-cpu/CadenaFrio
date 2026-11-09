using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Junta las cifras de un día (en hora de Perú) y las manda por Telegram a quienes reciben alertas.
// Las cifras salen de CalculoIndicadores: son las mismas que muestra Reportes para ese día.
public class ResumenDiario
{
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    private readonly CadenaFrioDbContext _db;
    private readonly CalculoIndicadores _calculo;
    private readonly ClienteTelegram _telegram;
    private readonly OpcionesEstablecimiento _establecimiento;

    public ResumenDiario(CadenaFrioDbContext db, CalculoIndicadores calculo, ClienteTelegram telegram,
        IOptions<OpcionesEstablecimiento> establecimiento)
    {
        _db = db;
        _calculo = calculo;
        _telegram = telegram;
        _establecimiento = establecimiento.Value;
    }

    // Día de Perú que corresponde a un instante UTC.
    public static DateOnly DiaPeru(DateTime utc) => DateOnly.FromDateTime(utc + DesfasePeru);

    // Próximo instante (UTC) en que el reloj de Perú marca la hora indicada, después de "ahora".
    public static DateTime ProximoEnvio(DateTime ahoraUtc, int hora)
    {
        var ahoraPeru = ahoraUtc + DesfasePeru;
        var envioPeru = ahoraPeru.Date.AddHours(hora);
        if (envioPeru <= ahoraPeru)
        {
            envioPeru = envioPeru.AddDays(1);
        }
        return DateTime.SpecifyKind(envioPeru - DesfasePeru, DateTimeKind.Utc);
    }

    // Texto del resumen del día indicado. "ahora" sirve para el plazo de las pendientes y para no
    // esperar lecturas que todavía no debían llegar (si se pide el resumen de hoy).
    public async Task<string> PrepararAsync(DateOnly dia, DateTime ahoraUtc)
    {
        // El día de Perú va de las 00:00 a las 24:00, es decir de las 05:00 a las 05:00 UTC.
        var desde = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DesfasePeru;
        var hasta = desde.AddDays(1);

        var registro = await _db.Equipos.AsNoTracking().Where(e => e.Activo)
            .ToDictionaryAsync(e => e.Id, e => e.FechaRegistro);

        var equipos = (await _calculo.ResumenPorEquipoAsync(desde, hasta))
            .Select(r =>
            {
                // Una lectura por minuto, desde que el equipo existe y hasta ahora si el día no terminó.
                var inicio = registro[r.EquipoId] > desde ? registro[r.EquipoId] : desde;
                var fin = hasta < ahoraUtc ? hasta : ahoraUtc;
                var esperadas = (int)Math.Max(0, (fin - inicio).TotalMinutes);
                return new EquipoDelResumen(r.Nombre, r.Lecturas, esperadas, r.Minima, r.Maxima, r.PorcentajeOptimo, r.Rupturas);
            })
            .ToList();

        // Las mismas pendientes que /alertas: exigen revisión y nadie la hizo (de cualquier fecha).
        var alertas = await _db.Alertas.AsNoTracking()
            .Where(a => a.Equipo.Activo && a.RevisionRequeridaDesde != null
                        && (a.FechaAtencion == null || a.FechaAtencion < a.RevisionRequeridaDesde))
            .OrderBy(a => a.Inicio)
            .Select(a => new { a.EquipoId, Equipo = a.Equipo.Nombre, a.Tipo, a.Nivel, a.FechaRuptura })
            .ToListAsync();

        var pendientes = new List<PendienteDelResumen>();
        foreach (var a in alertas)
        {
            DateTime? plazo = null;
            if (CatalogoAnexo13.Aplica(a.Tipo, a.Nivel))
            {
                // 24 h desde que se detectó la ruptura (numeral 6.10.2.1).
                plazo = await _db.ObtenerDeteccionRupturaAsync(a.EquipoId, a.FechaRuptura) + CatalogoAnexo13.PlazoNotificacion;
            }
            pendientes.Add(new PendienteDelResumen(a.Equipo, Describir(a.Tipo), plazo));
        }

        return MensajeResumenDiario.Armar(dia, _establecimiento.Nombre, equipos, pendientes, ahoraUtc);
    }

    // Envía el resumen a todos los usuarios activos con Telegram vinculado que reciben alertas.
    // Devuelve a cuántos se envió.
    public async Task<int> EnviarAsync(DateOnly dia, DateTime ahoraUtc, CancellationToken cancelar = default)
    {
        if (!_telegram.Configurado)
        {
            return 0;
        }

        var chats = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && u.RecibeAlertas && u.TelegramChatId != null)
            .Select(u => u.TelegramChatId!.Value)
            .ToListAsync(cancelar);
        if (chats.Count == 0)
        {
            return 0;
        }

        var texto = await PrepararAsync(dia, ahoraUtc);
        foreach (var chat in chats)
        {
            await _telegram.EnviarAsync(chat, texto, cancelacion: cancelar);
        }
        return chats.Count;
    }

    private static string Describir(TipoAlerta tipo) => tipo switch
    {
        TipoAlerta.SinSenal => "sin señal",
        TipoAlerta.SensorSinVariacion => "sensor sin variación",
        _ => "ruptura"
    };
}
