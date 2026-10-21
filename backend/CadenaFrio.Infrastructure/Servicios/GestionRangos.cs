using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

// Resultado de asignar vacunas: si se pudo, el rango nuevo (si cambió); si no, el motivo.
public record ResultadoAsignacion(bool Exito, string? Error, RangoTemperatura? RangoNuevo);

// Calcula el rango de cada equipo a partir de las vacunas que guarda.
public class GestionRangos
{
    private readonly CadenaFrioDbContext _db;

    public GestionRangos(CadenaFrioDbContext db)
    {
        _db = db;
    }

    private static LimitesTemperatura Limites(Vacuna v) =>
        new(v.OptimoMinimo, v.OptimoMaximo, v.LimiteMinimo, v.LimiteMaximo);

    // Asigna las vacunas que guarda un equipo y registra el rango calculado.
    public async Task<ResultadoAsignacion> AsignarVacunasAsync(int equipoId, IReadOnlyCollection<int> vacunaIds, string usuario)
    {
        var ids = vacunaIds.Distinct().ToList();
        var vacunas = await _db.Vacunas.Where(v => ids.Contains(v.Id) && v.Activo).ToListAsync();

        if (vacunas.Count != ids.Count)
        {
            return new(false, "Alguna de las vacunas elegidas no existe o fue retirada del catálogo.", null);
        }

        LimitesTemperatura? limites = null;
        if (vacunas.Count > 0)
        {
            limites = LimitesTemperatura.Interseccion(vacunas.Select(Limites));
            if (limites is null)
            {
                return new(false,
                    $"Estas vacunas no pueden almacenarse juntas: no existe una temperatura segura para todas ({string.Join(", ", vacunas.Select(v => v.Nombre))}).",
                    null);
            }
        }

        // Se quitan las que ya no guarda y se agregan las nuevas. (No se borran y vuelven a
        // crear las mismas: Entity Framework no permite eliminar y agregar la misma clave a la vez.)
        var actuales = await _db.EquipoVacunas.Where(a => a.EquipoId == equipoId).ToListAsync();
        _db.EquipoVacunas.RemoveRange(actuales.Where(a => !ids.Contains(a.VacunaId)));
        _db.EquipoVacunas.AddRange(ids
            .Where(id => actuales.All(a => a.VacunaId != id))
            .Select(id => new EquipoVacuna { EquipoId = equipoId, VacunaId = id }));

        // Sin vacunas, el equipo conserva su último rango (puede ajustarse a mano).
        RangoTemperatura? nuevo = null;
        if (limites is not null)
        {
            var detalle = $"Calculado según: {string.Join(", ", vacunas.OrderBy(v => v.Nombre).Select(v => v.Nombre))}";
            nuevo = await RegistrarSiCambioAsync(equipoId, limites, detalle, usuario);
        }

        await _db.SaveChangesAsync();
        return new(true, null, nuevo);
    }

    // Antes de editar una vacuna: comprueba que todos los equipos que la guardan sigan
    // teniendo un rango común con sus demás vacunas. Devuelve el problema, o null si todo está bien.
    public async Task<string?> ValidarCambioDeVacunaAsync(int vacunaId, LimitesTemperatura nuevos)
    {
        var equipos = await EquiposConVacunaAsync(vacunaId);
        var incompatibles = equipos
            .Where(e => LimitesTemperatura.Interseccion(
                e.Vacunas.Select(a => a.VacunaId == vacunaId ? nuevos : Limites(a.Vacuna))) is null)
            .Select(e => e.Nombre)
            .ToList();

        return incompatibles.Count == 0
            ? null
            : $"Con estos rangos, la vacuna ya no sería compatible con las demás vacunas de: {string.Join(", ", incompatibles)}.";
    }

    // Después de editar una vacuna: recalcula los equipos que la guardan.
    // Así, si cambia la norma, basta con corregir la vacuna una vez.
    public async Task RecalcularEquiposDeVacunaAsync(int vacunaId, string usuario)
    {
        var equipos = await EquiposConVacunaAsync(vacunaId);
        foreach (var equipo in equipos)
        {
            var vacunas = equipo.Vacunas.Select(a => a.Vacuna).ToList();
            var limites = LimitesTemperatura.Interseccion(vacunas.Select(Limites));
            if (limites is null)
            {
                continue;
            }

            var nombre = vacunas.First(v => v.Id == vacunaId).Nombre;
            var detalle = $"Recalculado por cambio en \"{nombre}\". Según: {string.Join(", ", vacunas.OrderBy(v => v.Nombre).Select(v => v.Nombre))}";
            await RegistrarSiCambioAsync(equipo.Id, limites, detalle, usuario);
        }

        await _db.SaveChangesAsync();
    }

    private Task<List<Equipo>> EquiposConVacunaAsync(int vacunaId) =>
        _db.Equipos
            .Include(e => e.Vacunas).ThenInclude(a => a.Vacuna)
            .Where(e => e.Vacunas.Any(a => a.VacunaId == vacunaId))
            .ToListAsync();

    // Solo se registra un rango nuevo si cambia respecto al vigente: el historial
    // contiene únicamente cambios reales.
    private async Task<RangoTemperatura?> RegistrarSiCambioAsync(int equipoId, LimitesTemperatura limites, string detalle, string usuario)
    {
        var vigente = await _db.ObtenerRangoVigenteAsync(equipoId);
        if (vigente is not null
            && new LimitesTemperatura(vigente.OptimoMinimo, vigente.OptimoMaximo, vigente.LimiteMinimo, vigente.LimiteMaximo) == limites)
        {
            return null;
        }

        var rango = new RangoTemperatura
        {
            EquipoId = equipoId,
            OptimoMinimo = limites.OptimoMinimo,
            OptimoMaximo = limites.OptimoMaximo,
            LimiteMinimo = limites.LimiteMinimo,
            LimiteMaximo = limites.LimiteMaximo,
            FechaCambio = DateTime.UtcNow,
            ModificadoPor = usuario,
            Detalle = detalle.Length > 300 ? detalle[..297] + "..." : detalle
        };

        _db.RangosTemperatura.Add(rango);
        return rango;
    }
}
