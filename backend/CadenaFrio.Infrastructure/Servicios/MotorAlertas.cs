using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

// Regla de negocio de la HU03: decide si una lectura abre, escala, actualiza o cierra
// una alerta. Está separado del controlador para poder reutilizarlo (por ejemplo,
// con las lecturas del termo en el Sprint 7) y probarlo de forma independiente.
public class MotorAlertas
{
    private readonly CadenaFrioDbContext _db;
    private readonly INotificadorAlertas _notificador;

    public MotorAlertas(CadenaFrioDbContext db, INotificadorAlertas notificador)
    {
        _db = db;
        _notificador = notificador;
    }

    // Evalúa una lectura que ya fue guardada.
    public async Task EvaluarAsync(Lectura lectura)
    {
        // Si ya existe una lectura medida después de esta, se trata de una lectura
        // sincronizada tarde (HU06). No se usa para abrir ni cerrar alertas, para no
        // alterar el estado actual con información del pasado. Se tratará en el Sprint 4.
        var esLecturaAtrasada = await _db.Lecturas.AnyAsync(l =>
            l.EquipoId == lectura.EquipoId && l.FechaMedicion > lectura.FechaMedicion);

        if (esLecturaAtrasada)
        {
            return;
        }

        var limites = await _db.ObtenerLimitesAsync(lectura.EquipoId);

        // Dirección (alta o baja) y nivel (advertencia o ruptura) de la lectura.
        // Ambos nulos si está dentro del rango óptimo.
        var (tipo, nivel) = limites.Clasificar(lectura.Temperatura);

        var alertaAbierta = await _db.ObtenerAlertaTemperaturaAbiertaAsync(lectura.EquipoId);

        Alerta? alertaCerrada = null;
        Alerta? alertaNueva = null;
        Alerta? alertaEscalada = null;

        // 1. Cerrar: hay una alerta abierta y la dirección ya no es la misma
        //    (la temperatura volvió al rango óptimo, o saltó de alta a baja).
        if (alertaAbierta is not null && alertaAbierta.Tipo != tipo)
        {
            alertaAbierta.Fin = lectura.FechaMedicion;
            alertaCerrada = alertaAbierta;
        }

        if (tipo is not null)
        {
            if (alertaAbierta is not null && alertaAbierta.Tipo == tipo)
            {
                // 2. Actualizar: el episodio continúa; se guarda el valor más extremo alcanzado.
                alertaAbierta.ValorExtremo = tipo == TipoAlerta.TemperaturaAlta
                    ? Math.Max(alertaAbierta.ValorExtremo, lectura.Temperatura)
                    : Math.Min(alertaAbierta.ValorExtremo, lectura.Temperatura);

                // 3. Escalar: de advertencia a ruptura. Nunca baja de nivel.
                if (nivel == NivelAlerta.Ruptura && alertaAbierta.Nivel == NivelAlerta.Advertencia)
                {
                    alertaAbierta.Nivel = NivelAlerta.Ruptura;
                    alertaAbierta.FechaRuptura = lectura.FechaMedicion;
                    alertaEscalada = alertaAbierta;
                }
            }
            else
            {
                // 4. Abrir: alerta inmediata con la primera lectura fuera del rango óptimo.
                //    Si la temperatura ya cruzó el límite, se abre directamente como ruptura.
                alertaNueva = new Alerta
                {
                    EquipoId = lectura.EquipoId,
                    Tipo = tipo.Value,
                    Nivel = nivel!.Value,
                    Inicio = lectura.FechaMedicion,
                    FechaRuptura = nivel == NivelAlerta.Ruptura ? lectura.FechaMedicion : null,
                    ValorExtremo = lectura.Temperatura,
                    OptimoMinimo = limites.OptimoMinimo,
                    OptimoMaximo = limites.OptimoMaximo,
                    LimiteMinimo = limites.LimiteMinimo,
                    LimiteMaximo = limites.LimiteMaximo
                };
                _db.Alertas.Add(alertaNueva);
            }
        }

        await _db.SaveChangesAsync();

        // Se notifica después de guardar: solo se avisa de lo que quedó registrado.
        if (alertaCerrada is null && alertaNueva is null && alertaEscalada is null)
        {
            return;
        }

        var nombreEquipo = await _db.Equipos
            .Where(e => e.Id == lectura.EquipoId)
            .Select(e => e.Nombre)
            .FirstAsync();

        if (alertaCerrada is not null)
        {
            await _notificador.AlertaCerradaAsync(alertaCerrada, nombreEquipo);
        }

        if (alertaNueva is not null)
        {
            await _notificador.AlertaAbiertaAsync(alertaNueva, nombreEquipo);
        }

        if (alertaEscalada is not null)
        {
            await _notificador.AlertaEscaladaAsync(alertaEscalada, nombreEquipo);
        }
    }
}
