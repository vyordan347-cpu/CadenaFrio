using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Notificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Servicios;

// Escalamiento: si nadie presiona "Lo estoy atendiendo" a tiempo, se avisa a los
// contactos de escalamiento (por ejemplo, el jefe del establecimiento). Se avisa una sola vez.
public class EscaladorAlertas
{
    private readonly CadenaFrioDbContext _db;
    private readonly ClienteTelegram _telegram;
    private readonly NotificadorCorreo _correo;
    private readonly OpcionesNotificaciones _opciones;
    private readonly ILogger<EscaladorAlertas> _logger;

    public EscaladorAlertas(CadenaFrioDbContext db, ClienteTelegram telegram, NotificadorCorreo correo,
        IOptions<OpcionesNotificaciones> opciones, ILogger<EscaladorAlertas> logger)
    {
        _db = db;
        _telegram = telegram;
        _correo = correo;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task RevisarAsync(CancellationToken cancelacion)
    {
        var limite = DateTime.UtcNow.AddMinutes(-_opciones.MinutosParaEscalar);

        // Alertas urgentes y activas, sin atender, sin escalar y que superaron el tiempo.
        var pendientes = await _db.Alertas
            .Include(a => a.Equipo)
            .Where(a => a.Fin == null && a.ReconocidaPor == null && a.FechaEscalamiento == null
                        && (a.Nivel == NivelAlerta.Ruptura || a.Tipo == TipoAlerta.SinSenal || a.Tipo == TipoAlerta.SensorSinVariacion)
                        && (a.FechaRuptura ?? a.Inicio) <= limite)
            .ToListAsync(cancelacion);

        if (pendientes.Count == 0)
        {
            return;
        }

        var chats = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo && u.EsContactoEscalamiento && u.TelegramChatId != null)
            .Select(u => u.TelegramChatId!.Value)
            .ToListAsync(cancelacion);
        var correos = await _correo.DestinatariosAsync(soloEscalamiento: true);

        foreach (var alerta in pendientes)
        {
            var (titulo, cuerpo) = MensajesAlerta.Escalamiento(alerta, alerta.Equipo.Nombre, _opciones.MinutosParaEscalar);

            try
            {
                if (_telegram.Configurado)
                {
                    foreach (var chat in chats)
                    {
                        await _telegram.EnviarAsync(chat, $"<b>{titulo}</b>\n\n{cuerpo}",
                            "✅ Lo estoy atendiendo", $"{NotificadorTelegram.PrefijoBoton}{alerta.Id}", cancelacion);
                    }
                }
                await _correo.EnviarAsync(correos, titulo, cuerpo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el escalamiento de la alerta {Id}.", alerta.Id);
            }

            // Se marca igual: el error queda en el registro y no se reintenta cada minuto.
            alerta.FechaEscalamiento = DateTime.UtcNow;
            _logger.LogError("ESCALAMIENTO | {Equipo} | alerta {Id} sin atender desde hace {Minutos} min",
                alerta.Equipo.Nombre, alerta.Id, _opciones.MinutosParaEscalar);
        }

        await _db.SaveChangesAsync(cancelacion);
    }
}
