using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Data;

// Consultas que se usan en varios lugares (controladores y motor de alertas).
// Escribirlas una sola vez evita que la misma regla quede repetida y luego se desincronice.
public static class ConsultasCadenaFrio
{
    // El rango vigente es el más reciente. El Id desempata si dos cambios
    // tuvieran exactamente la misma fecha. Nulo si nunca se configuró.
    public static Task<RangoTemperatura?> ObtenerRangoVigenteAsync(this CadenaFrioDbContext db, int equipoId) =>
        db.RangosTemperatura
            .AsNoTracking()
            .Where(r => r.EquipoId == equipoId)
            .OrderByDescending(r => r.FechaCambio)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync();

    // Límites a aplicar: los del rango vigente o, si no hay, los de por defecto (RF04).
    public static async Task<(double Minimo, double Maximo)> ObtenerLimitesAsync(this CadenaFrioDbContext db, int equipoId)
    {
        var rango = await db.ObtenerRangoVigenteAsync(equipoId);
        return rango is null
            ? (RangoTemperatura.MinimaPorDefecto, RangoTemperatura.MaximaPorDefecto)
            : (rango.TemperaturaMinima, rango.TemperaturaMaxima);
    }

    // Alerta de temperatura (alta o baja) que sigue activa en el equipo, si la hay.
    public static Task<Alerta?> ObtenerAlertaTemperaturaAbiertaAsync(this CadenaFrioDbContext db, int equipoId) =>
        db.Alertas
            .Where(a => a.EquipoId == equipoId
                        && a.Fin == null
                        && (a.Tipo == TipoAlerta.TemperaturaAlta || a.Tipo == TipoAlerta.TemperaturaBaja))
            .FirstOrDefaultAsync();
}
