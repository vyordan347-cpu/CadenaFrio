using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;

namespace CadenaFrio.Api.Vigilancia;

// Proceso en segundo plano: respaldo automático diario de la base (Fase 5c).
// Revisa al encender la API y luego cada hora; RespaldoBaseDatos decide si corresponde.
public class RespaldoDiario : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _fabricaAlcances;
    private readonly ILogger<RespaldoDiario> _logger;

    public RespaldoDiario(IServiceScopeFactory fabricaAlcances, ILogger<RespaldoDiario> logger)
    {
        _fabricaAlcances = fabricaAlcances;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        // Primera revisión al encender: si la PC estuvo apagada a la hora del respaldo, se hace ahora.
        do
        {
            try
            {
                using var alcance = _fabricaAlcances.CreateScope();
                var respaldos = alcance.ServiceProvider.GetRequiredService<RespaldoBaseDatos>();

                if (respaldos.CrearAutomaticoSiCorresponde() is { } archivo)
                {
                    var bitacora = alcance.ServiceProvider.GetRequiredService<Bitacora>();
                    await bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Administracion,
                        "Respaldo automático de la base", "Sistema", archivo.Nombre);
                    _logger.LogInformation("Respaldo automático creado: {Archivo}", archivo.Nombre);
                }
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                // La API se está apagando.
            }
            catch (Exception ex)
            {
                // Un error (por ejemplo, disco lleno) no debe detener los respaldos de los días siguientes.
                _logger.LogError(ex, "No se pudo crear el respaldo automático de la base.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(detener));
    }
}
