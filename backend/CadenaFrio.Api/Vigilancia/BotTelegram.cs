using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Notificaciones;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Vigilancia;

// Escucha lo que llega al bot de Telegram (long polling) y responde:
// - "/start CÓDIGO": vincula el chat con un usuario del sistema.
// - Botón "Lo estoy atendiendo": registra quién atiende la alerta.
public class BotTelegram : BackgroundService
{
    private readonly IServiceScopeFactory _fabricaAlcances;
    private readonly ILogger<BotTelegram> _logger;

    public BotTelegram(IServiceScopeFactory fabricaAlcances, ILogger<BotTelegram> logger)
    {
        _fabricaAlcances = fabricaAlcances;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        long siguiente = 0;

        while (!detener.IsCancellationRequested)
        {
            try
            {
                using var alcance = _fabricaAlcances.CreateScope();
                var telegram = alcance.ServiceProvider.GetRequiredService<ClienteTelegram>();

                if (!telegram.Configurado)
                {
                    // Sin token configurado, el bot queda en espera.
                    await Task.Delay(TimeSpan.FromMinutes(1), detener);
                    continue;
                }

                foreach (var actualizacion in await telegram.ObtenerActualizacionesAsync(siguiente, detener))
                {
                    siguiente = actualizacion.Id + 1;
                    await ProcesarAsync(actualizacion, alcance.ServiceProvider, telegram, detener);
                }
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sin internet o con un error de Telegram: se reintenta en unos segundos.
                _logger.LogWarning("Bot de Telegram: {Error}. Reintentando…", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), detener);
            }
        }
    }

    private static async Task ProcesarAsync(ActualizacionTelegram actualizacion, IServiceProvider servicios,
        ClienteTelegram telegram, CancellationToken cancelacion)
    {
        var db = servicios.GetRequiredService<CadenaFrioDbContext>();

        // 1. Vinculación: el usuario abrió el enlace del sistema, que envía "/start CÓDIGO".
        if (actualizacion.Mensaje is { Texto: { } texto } mensaje && texto.StartsWith("/start"))
        {
            var codigo = texto.Length > 7 ? texto[7..].Trim() : "";
            var usuario = codigo.Length > 0
                ? await db.Usuarios.FirstOrDefaultAsync(u => u.CodigoVinculacionTelegram == codigo && u.Activo, cancelacion)
                : null;

            if (usuario is null)
            {
                await telegram.EnviarAsync(mensaje.Chat.Id,
                    "No reconozco este enlace. Genere uno nuevo en el sistema: menú de su cuenta → <b>Notificaciones</b>.",
                    cancelacion: cancelacion);
                return;
            }

            usuario.TelegramChatId = mensaje.Chat.Id;
            usuario.CodigoVinculacionTelegram = null;
            await db.SaveChangesAsync(cancelacion);

            await telegram.EnviarAsync(mensaje.Chat.Id,
                $"✅ Listo, <b>{System.Net.WebUtility.HtmlEncode(usuario.NombreCompleto)}</b>. Este chat recibirá los avisos de la cadena de frío.",
                cancelacion: cancelacion);
            return;
        }

        // 2. Botón "Lo estoy atendiendo".
        if (actualizacion.Boton is { Dato: { } dato } boton && dato.StartsWith(NotificadorTelegram.PrefijoBoton)
            && int.TryParse(dato[NotificadorTelegram.PrefijoBoton.Length..], out var alertaId) && boton.Mensaje is { } original)
        {
            // Solo un usuario vinculado y activo puede confirmar la atención.
            var usuario = await db.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TelegramChatId == original.Chat.Id && u.Activo, cancelacion);
            if (usuario is null)
            {
                await telegram.ResponderBotonAsync(boton.Id, "Este chat no está vinculado a un usuario activo.", cancelacion);
                return;
            }

            var reconocimiento = servicios.GetRequiredService<ReconocimientoAlertas>();
            var resultado = await reconocimiento.ReconocerAsync(alertaId, usuario.NombreCompleto, original.Chat.Id);
            await telegram.ResponderBotonAsync(boton.Id, resultado.Mensaje, cancelacion);

            if (resultado.Exito)
            {
                await telegram.EditarAsync(original.Chat.Id, original.Id,
                    $"✅ <b>Usted está atendiendo</b> la alerta de <b>{System.Net.WebUtility.HtmlEncode(resultado.NombreEquipo ?? "")}</b> " +
                    $"desde las {MensajesAlerta.Hora(DateTime.UtcNow)}.\nCuando termine, registre la revisión en el sistema.",
                    cancelacion);
            }
        }
    }
}
