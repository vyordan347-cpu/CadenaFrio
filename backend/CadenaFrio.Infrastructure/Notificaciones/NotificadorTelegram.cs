using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Aviso inmediato por Telegram, con el botón "Lo estoy atendiendo".
public class NotificadorTelegram : INotificadorAlertas
{
    public const string PrefijoBoton = "atender:";

    private readonly ClienteTelegram _telegram;
    private readonly CadenaFrioDbContext _db;

    public NotificadorTelegram(ClienteTelegram telegram, CadenaFrioDbContext db)
    {
        _telegram = telegram;
        _db = db;
    }

    public Task AlertaAbiertaAsync(Alerta alerta, string nombreEquipo) =>
        MensajesAlerta.EsUrgente(alerta) ? EnviarAperturaAsync(alerta, nombreEquipo) : Task.CompletedTask;

    // Una advertencia que escala a ruptura, o un silencio que se prolonga, también se avisa.
    public Task AlertaEscaladaAsync(Alerta alerta, string nombreEquipo) => EnviarAperturaAsync(alerta, nombreEquipo);

    public async Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo)
    {
        if (!_telegram.Configurado || !MensajesAlerta.EsUrgente(alerta))
        {
            return;
        }

        foreach (var chat in await DestinatariosAsync())
        {
            await _telegram.EnviarAsync(chat, MensajesAlerta.Cierre(alerta, nombreEquipo));
        }
    }

    private async Task EnviarAperturaAsync(Alerta alerta, string nombreEquipo)
    {
        if (!_telegram.Configurado)
        {
            return;
        }

        var (titulo, cuerpo) = MensajesAlerta.Apertura(alerta, nombreEquipo);
        foreach (var chat in await DestinatariosAsync())
        {
            await _telegram.EnviarAsync(chat, $"<b>{titulo}</b>\n\n{cuerpo}",
                "✅ Lo estoy atendiendo", $"{PrefijoBoton}{alerta.Id}");
        }
    }

    private Task<List<long>> DestinatariosAsync() =>
        _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo && u.RecibeAlertas && u.TelegramChatId != null)
            .Select(u => u.TelegramChatId!.Value)
            .ToListAsync();
}
