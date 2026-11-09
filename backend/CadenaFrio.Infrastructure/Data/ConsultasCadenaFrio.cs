using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Data;

// Consultas que se usan en varios lugares (controladores y motor de alertas).
// Escribirlas una sola vez evita que la misma regla quede repetida y luego se desincronice.
public static class ConsultasCadenaFrio
{
    // Cuándo se detectó una ruptura: la hora en que llegó al servidor la lectura que la provocó
    // (la medida en FechaRuptura). En vivo es casi la misma hora de la ruptura; si el equipo estuvo
    // sin conexión y la envió al reconectarse, es la hora de la reconexión. De ahí corren las 24 h
    // para notificar (NTS 242: 6.10.1 "en cuanto se detecta" y "Fecha en que detectó" del Anexo 13).
    // Si no encuentra la lectura, devuelve la hora de la ruptura (el criterio de antes).
    public static async Task<DateTime?> ObtenerDeteccionRupturaAsync(this CadenaFrioDbContext db, int equipoId, DateTime? fechaRuptura)
    {
        if (fechaRuptura is not { } ruptura)
        {
            return null;
        }

        var recepcion = await db.Lecturas
            .AsNoTracking()
            .Where(l => l.EquipoId == equipoId && l.FechaMedicion == ruptura)
            .OrderBy(l => l.FechaRecepcion)
            .Select(l => (DateTime?)l.FechaRecepcion)
            .FirstOrDefaultAsync();

        return recepcion is { } r && r > ruptura ? r : ruptura;
    }

    // El rango vigente es el más reciente. El Id desempata si dos cambios
    // tuvieran exactamente la misma fecha. Nulo si nunca se configuró.
    public static Task<RangoTemperatura?> ObtenerRangoVigenteAsync(this CadenaFrioDbContext db, int equipoId) =>
        db.RangosTemperatura
            .AsNoTracking()
            .Where(r => r.EquipoId == equipoId)
            .OrderByDescending(r => r.FechaCambio)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync();

    // Rangos a aplicar: los del rango vigente o, si no hay, los de por defecto (RF04).
    public static async Task<LimitesTemperatura> ObtenerLimitesAsync(this CadenaFrioDbContext db, int equipoId)
    {
        var rango = await db.ObtenerRangoVigenteAsync(equipoId);
        return rango is null
            ? LimitesTemperatura.PorDefecto
            : new LimitesTemperatura(rango.OptimoMinimo, rango.OptimoMaximo, rango.LimiteMinimo, rango.LimiteMaximo);
    }

    // Alerta de temperatura (alta o baja) que sigue activa en el equipo, si la hay.
    public static Task<Alerta?> ObtenerAlertaTemperaturaAbiertaAsync(this CadenaFrioDbContext db, int equipoId) =>
        db.Alertas
            .Where(a => a.EquipoId == equipoId
                        && a.Fin == null
                        && (a.Tipo == TipoAlerta.TemperaturaAlta || a.Tipo == TipoAlerta.TemperaturaBaja))
            .FirstOrDefaultAsync();
}
