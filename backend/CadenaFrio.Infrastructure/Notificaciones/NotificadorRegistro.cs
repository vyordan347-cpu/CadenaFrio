using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
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
        if (alerta.Tipo == TipoAlerta.SinSenal)
        {
            _logger.LogWarning(
                "SIN SEÑAL | {Equipo} | sin datos desde {Inicio:HH:mm} UTC (posible corte de energía o falla de conexión) | última temperatura {Temperatura} °C",
                nombreEquipo, alerta.Inicio, alerta.ValorExtremo);
        }
        else
        {
            _logger.LogWarning(
                "ALERTA ABIERTA ({Nivel}) | {Equipo} | {Tipo} | {Temperatura} °C | óptimo {OptimoMin} a {OptimoMax} °C, límite {LimiteMin} a {LimiteMax} °C",
                alerta.Nivel, nombreEquipo, alerta.Tipo, alerta.ValorExtremo,
                alerta.OptimoMinimo, alerta.OptimoMaximo, alerta.LimiteMinimo, alerta.LimiteMaximo);
        }

        return Task.CompletedTask;
    }

    public Task AlertaEscaladaAsync(Alerta alerta, string nombreEquipo)
    {
        if (alerta.Tipo == TipoAlerta.SinSenal)
        {
            var silencio = alerta.RevisionRequeridaDesde!.Value - alerta.Inicio;

            _logger.LogError(
                "SILENCIO PROLONGADO | {Equipo} | {Minutos:0} min sin datos | verificar físicamente las vacunas",
                nombreEquipo, silencio.TotalMinutes);
        }
        else
        {
            var margen = alerta.FechaRuptura!.Value - alerta.Inicio;

            _logger.LogError(
                "RUPTURA DE CADENA DE FRÍO | {Equipo} | {Tipo} | {Temperatura} °C | {Minutos:0.#} min después de la advertencia",
                nombreEquipo, alerta.Tipo, alerta.ValorExtremo, margen.TotalMinutes);
        }

        return Task.CompletedTask;
    }

    public Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo)
    {
        var duracion = alerta.Fin!.Value - alerta.Inicio;

        if (alerta.Tipo == TipoAlerta.SinSenal)
        {
            _logger.LogInformation(
                "SEÑAL RECUPERADA | {Equipo} | estuvo {Minutos:0.#} min sin datos",
                nombreEquipo, duracion.TotalMinutes);
        }
        else
        {
            _logger.LogInformation(
                "ALERTA CERRADA ({Nivel}) | {Equipo} | {Tipo} | duró {Minutos:0.#} min | valor extremo {Temperatura} °C",
                alerta.Nivel, nombreEquipo, alerta.Tipo, duracion.TotalMinutes, alerta.ValorExtremo);
        }

        return Task.CompletedTask;
    }
}
