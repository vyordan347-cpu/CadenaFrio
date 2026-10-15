using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Servicios;
using Microsoft.Extensions.Logging;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Implementación provisional del canal de notificaciones: escribe el aviso
// en la consola del servidor. Se reemplazará por WhatsApp u otro canal
// cambiando una sola línea en Program.cs.
public class NotificadorRegistro : INotificadorAlertas
{
    private readonly ILogger<NotificadorRegistro> _logger;

    public NotificadorRegistro(ILogger<NotificadorRegistro> logger)
    {
        _logger = logger;
    }

    public Task AlertaAbiertaAsync(Alerta alerta, string nombreEquipo)
    {
        _logger.LogWarning(
            "ALERTA ABIERTA | {Equipo} | {Tipo} | {Temperatura} °C fuera del rango {Minimo} a {Maximo} °C",
            nombreEquipo, alerta.Tipo, alerta.ValorExtremo, alerta.RangoMinimo, alerta.RangoMaximo);

        return Task.CompletedTask;
    }

    public Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo)
    {
        var duracion = alerta.Fin!.Value - alerta.Inicio;

        _logger.LogInformation(
            "ALERTA CERRADA | {Equipo} | {Tipo} | duró {Minutos:0.#} min | valor extremo {Temperatura} °C",
            nombreEquipo, alerta.Tipo, duracion.TotalMinutes, alerta.ValorExtremo);

        return Task.CompletedTask;
    }
}
