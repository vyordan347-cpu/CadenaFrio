using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

// Regla de negocio de la HU03: decide si una lectura abre, actualiza o cierra una alerta.
// Está separado del controlador para poder reutilizarlo (por ejemplo, con las
// lecturas del termo en el Sprint 7) y probarlo de forma independiente.
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

        var (minimo, maximo) = await _db.ObtenerLimitesAsync(lectura.EquipoId);

        // Condición de la lectura: alta, baja o dentro del rango (null).
        TipoAlerta? condicion =
            lectura.Temperatura > maximo ? TipoAlerta.TemperaturaAlta :
            lectura.Temperatura < minimo ? TipoAlerta.TemperaturaBaja :
            null;

        var alertaAbierta = await _db.ObtenerAlertaTemperaturaAbiertaAsync(lectura.EquipoId);

        Alerta? alertaCerrada = null;
        Alerta? alertaNueva = null;

        // 1. Cerrar: hay una alerta abierta y la condición ya no es la misma
        //    (la temperatura volvió al rango, o saltó de alta a baja).
        if (alertaAbierta is not null && alertaAbierta.Tipo != condicion)
        {
            alertaAbierta.Fin = lectura.FechaMedicion;
            alertaCerrada = alertaAbierta;
        }

        if (condicion is not null)
        {
            if (alertaAbierta is not null && alertaAbierta.Tipo == condicion)
            {
                // 2. Actualizar: la alerta sigue; se guarda el valor más extremo alcanzado.
                alertaAbierta.ValorExtremo = condicion == TipoAlerta.TemperaturaAlta
                    ? Math.Max(alertaAbierta.ValorExtremo, lectura.Temperatura)
                    : Math.Min(alertaAbierta.ValorExtremo, lectura.Temperatura);
            }
            else
            {
                // 3. Abrir: alerta inmediata con la primera lectura fuera de rango.
                alertaNueva = new Alerta
                {
                    EquipoId = lectura.EquipoId,
                    Tipo = condicion.Value,
                    Inicio = lectura.FechaMedicion,
                    ValorExtremo = lectura.Temperatura,
                    RangoMinimo = minimo,
                    RangoMaximo = maximo
                };
                _db.Alertas.Add(alertaNueva);
            }
        }

        await _db.SaveChangesAsync();

        // Se notifica después de guardar: solo se avisa de lo que quedó registrado.
        if (alertaCerrada is null && alertaNueva is null)
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
    }
}
