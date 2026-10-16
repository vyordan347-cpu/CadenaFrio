using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Servicios;

// Regla de negocio de la HU03: decide si las lecturas abren, escalan, actualizan o
// cierran alertas. Procesa tanto una lectura en vivo como un lote sincronizado (HU06).
public class MotorAlertas
{
    private enum TipoEvento { Abierta, Escalada, Cerrada }

    private readonly CadenaFrioDbContext _db;
    private readonly INotificadorAlertas _notificador;
    private readonly OpcionesMonitoreo _opciones;

    public MotorAlertas(CadenaFrioDbContext db, INotificadorAlertas notificador, IOptions<OpcionesMonitoreo> opciones)
    {
        _db = db;
        _notificador = notificador;
        _opciones = opciones.Value;
    }

    // Evalúa lecturas nuevas que ya fueron guardadas.
    // ultimaMedicionPrevia: la medición más reciente que existía ANTES de guardarlas.
    public async Task EvaluarAsync(int equipoId, IReadOnlyList<Lectura> nuevas, DateTime? ultimaMedicionPrevia)
    {
        if (nuevas.Count == 0)
        {
            return;
        }

        // 0. Llegaron datos: si había una alerta de silencio, se cierra.
        await CerrarSinSenalAsync(equipoId, nuevas.Max(l => l.FechaRecepcion));

        var limites = await _db.ObtenerLimitesAsync(equipoId);
        var ordenadas = nuevas.OrderBy(l => l.FechaMedicion).ToList();

        // Se separan en dos grupos:
        // - Atrasadas: medidas antes de la última lectura que ya teníamos. Pertenecen al pasado
        //   y no deben alterar el estado actual: solo se reconstruyen sus episodios.
        // - Actuales: continúan la línea de tiempo y sí actualizan el estado actual.
        var atrasadas = ordenadas.Where(l => l.FechaMedicion <= ultimaMedicionPrevia).ToList();
        var actuales = ordenadas.Where(l => !(l.FechaMedicion <= ultimaMedicionPrevia)).ToList();

        var eventos = new List<(TipoEvento Tipo, Alerta Alerta)>();

        ReconstruirPasado(equipoId, atrasadas, limites, eventos);
        await AplicarActualesAsync(equipoId, actuales, limites, eventos);

        await _db.SaveChangesAsync();
        await NotificarAsync(equipoId, eventos);
    }

    // Reproduce las lecturas actuales en orden, como si hubieran llegado una por una.
    // Si el dispositivo estuvo sin conexión, aquí se descubre lo que pasó en ese tiempo.
    private async Task AplicarActualesAsync(
        int equipoId, List<Lectura> actuales, LimitesTemperatura limites, List<(TipoEvento, Alerta)> eventos)
    {
        var abierta = await _db.ObtenerAlertaTemperaturaAbiertaAsync(equipoId);

        foreach (var lectura in actuales)
        {
            var (tipo, nivel) = limites.Clasificar(lectura.Temperatura);

            // 1. Cerrar: la temperatura volvió al rango óptimo, o saltó de alta a baja.
            if (abierta is not null && abierta.Tipo != tipo)
            {
                abierta.Fin = lectura.FechaMedicion;
                eventos.Add((TipoEvento.Cerrada, abierta));

                // Se guarda de inmediato: si en este mismo lote se abre otra alerta del mismo
                // tipo, el índice único exige que la anterior ya figure como cerrada.
                await _db.SaveChangesAsync();
                abierta = null;
            }

            if (tipo is null)
            {
                continue;
            }

            if (abierta is not null)
            {
                // 2. Actualizar el valor extremo y, si corresponde, 3. escalar a ruptura.
                ActualizarEpisodio(abierta, lectura, nivel!.Value, eventos);
            }
            else
            {
                // 4. Abrir. Si la lectura llegó mucho después de medirse, el episodio
                //    se está conociendo tarde (venía guardada en el dispositivo).
                var esTardia = lectura.FechaRecepcion - lectura.FechaMedicion > _opciones.LimiteSinSenal;
                abierta = CrearAlerta(equipoId, tipo.Value, nivel!.Value, lectura, limites, esTardia);
                _db.Alertas.Add(abierta);
                eventos.Add((TipoEvento.Abierta, abierta));
            }
        }
    }

    // Reconstruye episodios ocurridos en el pasado a partir de lecturas atrasadas.
    // Siempre quedan cerrados y marcados como detectados tarde; el estado actual no cambia.
    private void ReconstruirPasado(
        int equipoId, List<Lectura> atrasadas, LimitesTemperatura limites, List<(TipoEvento, Alerta)> eventos)
    {
        Alerta? episodio = null;

        foreach (var lectura in atrasadas)
        {
            var (tipo, nivel) = limites.Clasificar(lectura.Temperatura);

            if (episodio is not null && episodio.Tipo != tipo)
            {
                episodio.Fin = lectura.FechaMedicion;
                episodio = null;
            }

            if (tipo is null)
            {
                continue;
            }

            if (episodio is not null)
            {
                ActualizarEpisodio(episodio, lectura, nivel!.Value, eventos);
            }
            else
            {
                episodio = CrearAlerta(equipoId, tipo.Value, nivel!.Value, lectura, limites, detectadaTarde: true);
                _db.Alertas.Add(episodio);
                eventos.Add((TipoEvento.Abierta, episodio));
            }
        }

        // Si las lecturas atrasadas terminan fuera de rango, el episodio se cierra con la
        // última de ellas: después ya había lecturas en vivo que muestran el estado real.
        if (episodio is not null)
        {
            episodio.Fin = atrasadas[^1].FechaMedicion;
        }
    }

    private static void ActualizarEpisodio(
        Alerta alerta, Lectura lectura, NivelAlerta nivel, List<(TipoEvento, Alerta)> eventos)
    {
        // El valor más extremo alcanzado durante el episodio.
        alerta.ValorExtremo = alerta.Tipo == TipoAlerta.TemperaturaAlta
            ? Math.Max(alerta.ValorExtremo, lectura.Temperatura)
            : Math.Min(alerta.ValorExtremo, lectura.Temperatura);

        // Escalar de advertencia a ruptura. Nunca baja de nivel.
        // Desde ese momento exige revisión del personal (RF08).
        if (nivel == NivelAlerta.Ruptura && alerta.Nivel == NivelAlerta.Advertencia)
        {
            alerta.Nivel = NivelAlerta.Ruptura;
            alerta.FechaRuptura = lectura.FechaMedicion;
            alerta.RevisionRequeridaDesde = lectura.FechaMedicion;
            eventos.Add((TipoEvento.Escalada, alerta));
        }
    }

    // Si la temperatura ya cruzó el límite, la alerta nace como ruptura.
    private static Alerta CrearAlerta(
        int equipoId, TipoAlerta tipo, NivelAlerta nivel, Lectura lectura, LimitesTemperatura limites, bool detectadaTarde) => new()
    {
        EquipoId = equipoId,
        Tipo = tipo,
        Nivel = nivel,
        Inicio = lectura.FechaMedicion,
        FechaRuptura = nivel == NivelAlerta.Ruptura ? lectura.FechaMedicion : null,
        RevisionRequeridaDesde = nivel == NivelAlerta.Ruptura ? lectura.FechaMedicion : null,
        DetectadaTarde = detectadaTarde,
        ValorExtremo = lectura.Temperatura,
        OptimoMinimo = limites.OptimoMinimo,
        OptimoMaximo = limites.OptimoMaximo,
        LimiteMinimo = limites.LimiteMinimo,
        LimiteMaximo = limites.LimiteMaximo
    };

    private async Task CerrarSinSenalAsync(int equipoId, DateTime fechaRecepcion)
    {
        var sinSenal = await _db.Alertas.FirstOrDefaultAsync(a =>
            a.EquipoId == equipoId && a.Tipo == TipoAlerta.SinSenal && a.Fin == null);

        if (sinSenal is null)
        {
            return;
        }

        // El silencio terminó cuando el servidor volvió a recibir datos.
        sinSenal.Fin = fechaRecepcion;
        await _db.SaveChangesAsync();
        await NotificarAsync(equipoId, new List<(TipoEvento, Alerta)> { (TipoEvento.Cerrada, sinSenal) });
    }

    // Se notifica después de guardar: solo se avisa de lo que quedó registrado.
    private async Task NotificarAsync(int equipoId, List<(TipoEvento Tipo, Alerta Alerta)> eventos)
    {
        if (eventos.Count == 0)
        {
            return;
        }

        var nombreEquipo = await _db.Equipos
            .Where(e => e.Id == equipoId)
            .Select(e => e.Nombre)
            .FirstAsync();

        foreach (var (tipo, alerta) in eventos)
        {
            switch (tipo)
            {
                case TipoEvento.Abierta:
                    await _notificador.AlertaAbiertaAsync(alerta, nombreEquipo);
                    break;
                case TipoEvento.Escalada:
                    await _notificador.AlertaEscaladaAsync(alerta, nombreEquipo);
                    break;
                case TipoEvento.Cerrada:
                    await _notificador.AlertaCerradaAsync(alerta, nombreEquipo);
                    break;
            }
        }
    }
}
