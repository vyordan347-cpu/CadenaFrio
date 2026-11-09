using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Notificaciones;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Api.Vigilancia;

// Proceso en segundo plano: cada día, a la hora configurada (07:00 de Perú por defecto), manda por
// Telegram el resumen del día anterior. Si el servidor está apagado a esa hora, ese día no se envía.
public class EnvioResumenDiario : BackgroundService
{
    private readonly IServiceScopeFactory _fabricaAlcances;
    private readonly OpcionesResumenDiario _opciones;
    private readonly ILogger<EnvioResumenDiario> _logger;

    public EnvioResumenDiario(IServiceScopeFactory fabricaAlcances, IOptions<OpcionesResumenDiario> opciones,
        ILogger<EnvioResumenDiario> logger)
    {
        _fabricaAlcances = fabricaAlcances;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        if (!_opciones.Activo)
        {
            _logger.LogInformation("Resumen diario por Telegram desactivado (ResumenDiario:Activo).");
            return;
        }

        var hora = Math.Clamp(_opciones.Hora, 0, 23);
        while (!detener.IsCancellationRequested)
        {
            var envio = ResumenDiario.ProximoEnvio(DateTime.UtcNow, hora);
            try
            {
                await Task.Delay(envio - DateTime.UtcNow, detener);

                using var alcance = _fabricaAlcances.CreateScope();
                var resumen = alcance.ServiceProvider.GetRequiredService<ResumenDiario>();
                var ahora = DateTime.UtcNow;
                var ayer = ResumenDiario.DiaPeru(ahora).AddDays(-1);
                var enviados = await resumen.EnviarAsync(ayer, ahora, detener);
                _logger.LogInformation("Resumen diario del {Dia:dd/MM/yyyy} enviado a {Cantidad} chats de Telegram.", ayer, enviados);
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                // La API se está apagando.
            }
            catch (Exception ex)
            {
                // Un error (por ejemplo, sin internet) no debe detener los resúmenes de los días siguientes.
                _logger.LogError(ex, "No se pudo enviar el resumen diario por Telegram.");
            }
        }
    }
}
