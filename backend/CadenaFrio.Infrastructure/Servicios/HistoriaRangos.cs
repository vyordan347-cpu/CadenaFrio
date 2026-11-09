using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

// Qué rango regía en cada momento para un equipo.
//
// Cada cambio de rango queda guardado con su fecha (RangoTemperatura.FechaCambio): el rango de
// una lectura es el último definido antes de su hora (o en esa misma hora). Si la lectura es
// anterior al primer rango guardado, rige el de la norma por defecto (RF04), como hacía el
// sistema en ese momento. Así una lectura se juzga con el rango que tenía cuando se tomó, y
// cambiar el rango hoy no cambia el juicio sobre lecturas de antes.
public sealed class HistoriaRangos
{
    // Un tramo de tiempo [Desde, Hasta) en el que rigió un mismo rango.
    public sealed record Tramo(DateTime Desde, DateTime Hasta, LimitesTemperatura Limites);

    // Cambios en orden: fecha desde la que rige cada rango. Si dos tienen la misma fecha,
    // vale el último guardado (mismo criterio que el rango vigente de la configuración).
    private readonly List<(DateTime Desde, LimitesTemperatura Limites)> _cambios;

    private HistoriaRangos(List<(DateTime, LimitesTemperatura)> cambios) => _cambios = cambios;

    // A partir de rangos ya leídos (sirve también para las pruebas).
    public static HistoriaRangos De(IEnumerable<RangoTemperatura> rangos) =>
        new(rangos
            .OrderBy(r => r.FechaCambio)
            .ThenBy(r => r.Id)
            .Select(r => (r.FechaCambio, Limites(r)))
            .ToList());

    // Rangos guardados antes de que existiera el óptimo (migración DosNiveles) tenían un solo
    // nivel: su óptimo era su límite. Algunas bases quedaron con el óptimo en 0–0; se leen con
    // la misma regla de esa migración (óptimo = límite), sin modificar la base.
    private static LimitesTemperatura Limites(RangoTemperatura r) =>
        r.OptimoMaximo > r.OptimoMinimo
            ? new LimitesTemperatura(r.OptimoMinimo, r.OptimoMaximo, r.LimiteMinimo, r.LimiteMaximo)
            : new LimitesTemperatura(r.LimiteMinimo, r.LimiteMaximo, r.LimiteMinimo, r.LimiteMaximo);

    public static async Task<HistoriaRangos> CargarAsync(CadenaFrioDbContext db, int equipoId) =>
        De(await db.RangosTemperatura.AsNoTracking().Where(r => r.EquipoId == equipoId).ToListAsync());

    // El rango que regía en esa fecha (UTC).
    public LimitesTemperatura En(DateTime fecha)
    {
        var limites = LimitesTemperatura.PorDefecto;
        foreach (var (desde, rango) in _cambios)
        {
            if (desde > fecha)
            {
                break;
            }
            limites = rango;
        }
        return limites;
    }

    // Cuántas lecturas estuvieron en el óptimo y cuántas fuera del límite, cada una juzgada
    // con el rango de su momento. Usa la misma regla de siempre (LimitesTemperatura.Clasificar):
    // en el óptimo = sin alerta; fuera del límite = ruptura.
    public sealed record Conteo(int EnOptimo, int FueraDelLimite);

    public Conteo Contar(IEnumerable<(DateTime Fecha, double Temperatura)> lecturas)
    {
        int enOptimo = 0, fueraDelLimite = 0;
        foreach (var (fecha, temperatura) in lecturas)
        {
            var nivel = En(fecha).Clasificar(temperatura).Nivel;
            if (nivel is null) enOptimo++;
            else if (nivel == NivelAlerta.Ruptura) fueraDelLimite++;
        }
        return new Conteo(enOptimo, fueraDelLimite);
    }

    // Los tramos de rango dentro de [desde, hasta), en orden y sin huecos. Si el rango no
    // cambió en el periodo, es un solo tramo. Tramos seguidos con los mismos valores (por
    // ejemplo, el mismo rango guardado dos veces) se unen: no es un cambio real.
    public List<Tramo> Tramos(DateTime desde, DateTime hasta)
    {
        var tramos = new List<Tramo>();
        if (hasta <= desde)
        {
            return tramos;
        }

        var inicio = desde;
        var limites = En(desde);
        foreach (var (cambio, rango) in _cambios)
        {
            if (cambio <= desde || cambio >= hasta)
            {
                continue;
            }
            tramos.Add(new Tramo(inicio, cambio, limites));
            inicio = cambio;
            limites = rango;
        }
        tramos.Add(new Tramo(inicio, hasta, limites));

        // Limpieza: sin tramos de duración cero (dos cambios a la misma hora) y uniendo los
        // seguidos que tienen el mismo rango.
        var limpios = new List<Tramo>();
        foreach (var tramo in tramos.Where(t => t.Hasta > t.Desde))
        {
            if (limpios.Count > 0 && limpios[^1].Limites == tramo.Limites)
            {
                limpios[^1] = limpios[^1] with { Hasta = tramo.Hasta };
            }
            else
            {
                limpios.Add(tramo);
            }
        }
        return limpios;
    }
}
