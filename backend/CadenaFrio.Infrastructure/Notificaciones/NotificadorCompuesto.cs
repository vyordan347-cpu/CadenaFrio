using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Servicios;
using Microsoft.Extensions.Logging;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Envía cada aviso por todos los canales: consola, Telegram y correo.
// Si un canal falla (por ejemplo, sin internet), los demás siguen funcionando
// y el error queda registrado: un aviso fallido nunca detiene el registro de lecturas.
public class NotificadorCompuesto : INotificadorAlertas
{
    private readonly IReadOnlyList<INotificadorAlertas> _canales;
    private readonly ILogger<NotificadorCompuesto> _logger;

    public NotificadorCompuesto(NotificadorRegistro registro, NotificadorTelegram telegram, NotificadorCorreo correo,
        ILogger<NotificadorCompuesto> logger)
    {
        _canales = new INotificadorAlertas[] { registro, telegram, correo };
        _logger = logger;
    }

    public Task AlertaAbiertaAsync(Alerta alerta, string nombreEquipo) =>
        EnTodosAsync(c => c.AlertaAbiertaAsync(alerta, nombreEquipo));

    public Task AlertaEscaladaAsync(Alerta alerta, string nombreEquipo) =>
        EnTodosAsync(c => c.AlertaEscaladaAsync(alerta, nombreEquipo));

    public Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo) =>
        EnTodosAsync(c => c.AlertaCerradaAsync(alerta, nombreEquipo));

    private async Task EnTodosAsync(Func<INotificadorAlertas, Task> accion)
    {
        foreach (var canal in _canales)
        {
            try
            {
                await accion(canal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el aviso por {Canal}.", canal.GetType().Name);
            }
        }
    }
}
