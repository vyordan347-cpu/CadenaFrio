using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Api.Vigilancia;

// Proceso en segundo plano: ASP.NET Core lo ejecuta por su cuenta mientras la API
// está encendida, sin que nadie lo llame y aunque nadie tenga la página abierta.
// Cada cierto tiempo le pide al DetectorSilencio que revise los equipos.
public class VigilanteSenal : BackgroundService
{
    private readonly IServiceScopeFactory _fabricaAlcances;
    private readonly ILogger<VigilanteSenal> _logger;
    private readonly OpcionesMonitoreo _opciones;

    public VigilanteSenal(
        IServiceScopeFactory fabricaAlcances,
        ILogger<VigilanteSenal> logger,
        IOptions<OpcionesMonitoreo> opciones)
    {
        _fabricaAlcances = fabricaAlcances;
        _logger = logger;
        _opciones = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        using var temporizador = new PeriodicTimer(_opciones.IntervaloRevision);

        while (await temporizador.WaitForNextTickAsync(detener))
        {
            try
            {
                // El vigilante vive mientras la API esté encendida, pero el DbContext
                // debe ser de corta duración: se crea un alcance nuevo en cada revisión.
                using var alcance = _fabricaAlcances.CreateScope();
                var detector = alcance.ServiceProvider.GetRequiredService<DetectorSilencio>();
                await detector.RevisarAsync(detener);
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                // La API se está apagando.
            }
            catch (Exception ex)
            {
                // Un error en una revisión no debe detener al vigilante para siempre.
                _logger.LogError(ex, "Error al revisar la señal de los equipos.");
            }
        }
    }
}
