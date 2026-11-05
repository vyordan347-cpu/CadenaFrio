using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

public record ResultadoIndicadores(
    double? PorcentajeOptimo,
    int Rupturas,
    double? MinutosPromedioRespuesta,
    double? PorcentajeDisponibilidad,
    int PendientesDeRevision);

// Una fila por equipo en el Excel de Reportes.
public record ResumenEquipo(
    int EquipoId,
    string Nombre,
    string Ubicacion,
    int Lecturas,
    double? PorcentajeOptimo,
    double? Minima,
    double? Maxima,
    double? Promedio,
    int Rupturas);

// Indicadores clave (sección 8 del documento: métricas) en un periodo cualquiera.
// Lo usan la página de Reportes y su Excel, así los dos muestran exactamente las mismas cifras.
public class CalculoIndicadores
{
    private readonly CadenaFrioDbContext _db;

    public CalculoIndicadores(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // Periodo [desde, hasta) en UTC: incluye "desde" y excluye "hasta", así un día va de las
    // 00:00 a las 00:00 del día siguiente sin contar dos veces el borde.
    // Sin equipoId: todos los equipos activos. Con equipoId: solo ese, aunque esté inactivo.
    public async Task<ResultadoIndicadores> CalcularAsync(DateTime desde, DateTime hasta, int? equipoId = null, DateTime? ahoraUtc = null)
    {
        var ahora = ahoraUtc ?? DateTime.UtcNow;

        var equipos = await EquiposAsync(equipoId);
        var ids = equipos.Select(e => e.Id).ToList();

        // 1. Tiempo en óptimo y disponibilidad: se cuentan en la base, sin traer las lecturas.
        long recibidas = 0, enOptimo = 0;
        double esperadas = 0;
        foreach (var equipo in equipos)
        {
            var limites = await _db.ObtenerLimitesAsync(equipo.Id);
            var lecturas = _db.Lecturas.Where(l => l.EquipoId == equipo.Id && l.FechaMedicion >= desde && l.FechaMedicion < hasta);

            recibidas += await lecturas.CountAsync();
            enOptimo += await lecturas.CountAsync(l => l.Temperatura >= limites.OptimoMinimo && l.Temperatura <= limites.OptimoMaximo);

            // Se espera una lectura por minuto desde que el equipo existe (o desde el inicio del
            // periodo) hasta el final del periodo, o hasta ahora si el periodo no terminó.
            var inicio = equipo.FechaRegistro > desde ? equipo.FechaRegistro : desde;
            var fin = hasta < ahora ? hasta : ahora;
            esperadas += Math.Max(0, (fin - inicio).TotalMinutes);
        }

        // 2. Rupturas de temperatura que empezaron en el periodo.
        var rupturas = await _db.Alertas.CountAsync(a =>
            ids.Contains(a.EquipoId) && a.Nivel == NivelAlerta.Ruptura && a.Tipo != TipoAlerta.SinSenal
            && a.Inicio >= desde && a.Inicio < hasta);

        // 3. Tiempo de respuesta: de que la alerta exige revisión a que alguien la atiende.
        var atenciones = await _db.Alertas
            .AsNoTracking()
            .Where(a => ids.Contains(a.EquipoId)
                        && a.RevisionRequeridaDesde != null && a.FechaAtencion != null
                        && a.FechaAtencion >= a.RevisionRequeridaDesde
                        && a.RevisionRequeridaDesde >= desde && a.RevisionRequeridaDesde < hasta)
            .Select(a => new { Desde = a.RevisionRequeridaDesde!.Value, Atencion = a.FechaAtencion!.Value })
            .ToListAsync();

        // 4. Pendientes de revisión: es el estado de hoy, de cualquier fecha.
        var pendientes = await _db.Alertas.CountAsync(a =>
            ids.Contains(a.EquipoId)
            && a.RevisionRequeridaDesde != null && (a.FechaAtencion == null || a.FechaAtencion < a.RevisionRequeridaDesde));

        return new ResultadoIndicadores(
            recibidas > 0 ? Math.Round(100.0 * enOptimo / recibidas, 1) : null,
            rupturas,
            atenciones.Count > 0 ? Math.Round(atenciones.Average(a => (a.Atencion - a.Desde).TotalMinutes), 1) : null,
            esperadas >= 1 ? Math.Round(Math.Min(100, 100.0 * recibidas / esperadas), 1) : null,
            pendientes);
    }

    // Cifras de cada equipo en el mismo periodo [desde, hasta), ordenados por nombre.
    public async Task<List<ResumenEquipo>> ResumenPorEquipoAsync(DateTime desde, DateTime hasta, int? equipoId = null)
    {
        var resumen = new List<ResumenEquipo>();
        foreach (var equipo in (await EquiposAsync(equipoId)).OrderBy(e => e.Nombre))
        {
            var limites = await _db.ObtenerLimitesAsync(equipo.Id);
            var lecturas = _db.Lecturas.Where(l => l.EquipoId == equipo.Id && l.FechaMedicion >= desde && l.FechaMedicion < hasta);

            // Mínimo, máximo y promedio en una sola consulta. (double?) evita un error cuando no hay lecturas.
            var cifras = await lecturas
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Cantidad = g.Count(),
                    EnOptimo = g.Count(l => l.Temperatura >= limites.OptimoMinimo && l.Temperatura <= limites.OptimoMaximo),
                    Minima = g.Min(l => (double?)l.Temperatura),
                    Maxima = g.Max(l => (double?)l.Temperatura),
                    Promedio = g.Average(l => (double?)l.Temperatura)
                })
                .FirstOrDefaultAsync();

            var rupturas = await _db.Alertas.CountAsync(a =>
                a.EquipoId == equipo.Id && a.Nivel == NivelAlerta.Ruptura && a.Tipo != TipoAlerta.SinSenal
                && a.Inicio >= desde && a.Inicio < hasta);

            resumen.Add(new ResumenEquipo(
                equipo.Id, equipo.Nombre, equipo.Ubicacion,
                cifras?.Cantidad ?? 0,
                cifras is { Cantidad: > 0 } ? Math.Round(100.0 * cifras.EnOptimo / cifras.Cantidad, 1) : null,
                cifras?.Minima, cifras?.Maxima,
                cifras?.Promedio is { } p ? Math.Round(p, 2) : null,
                rupturas));
        }

        return resumen;
    }

    // Sin equipoId: todos los activos. Con equipoId: solo ese, aunque esté inactivo.
    private Task<List<Domain.Entities.Equipo>> EquiposAsync(int? equipoId) =>
        _db.Equipos
            .AsNoTracking()
            .Where(e => equipoId == null ? e.Activo : e.Id == equipoId)
            .ToListAsync();
}
