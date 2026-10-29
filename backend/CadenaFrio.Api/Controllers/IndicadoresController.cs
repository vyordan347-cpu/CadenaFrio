using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Indicadores clave para el panel: miden de verdad las métricas que promete el documento.
[ApiController]
[Authorize]
[Route("api/indicadores")]
public class IndicadoresController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;

    public IndicadoresController(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // GET /api/indicadores?dias=7
    [HttpGet]
    public async Task<ActionResult<IndicadoresDto>> Obtener([FromQuery] int dias = 7)
    {
        dias = Math.Clamp(dias, 1, 90);
        var ahora = DateTime.UtcNow;
        var desde = ahora.AddDays(-dias);

        var equipos = await _db.Equipos.AsNoTracking().Where(e => e.Activo).ToListAsync();

        // 1. Tiempo en óptimo y disponibilidad: se cuentan en la base, sin traer las lecturas.
        long recibidas = 0, enOptimo = 0;
        double esperadas = 0;
        foreach (var equipo in equipos)
        {
            var limites = await _db.ObtenerLimitesAsync(equipo.Id);
            var lecturas = _db.Lecturas.Where(l => l.EquipoId == equipo.Id && l.FechaMedicion >= desde);

            recibidas += await lecturas.CountAsync();
            enOptimo += await lecturas.CountAsync(l => l.Temperatura >= limites.OptimoMinimo && l.Temperatura <= limites.OptimoMaximo);

            // Se esperan lecturas desde que el equipo existe (o desde el inicio del periodo):
            // un equipo registrado ayer no "debía" haber enviado datos hace una semana.
            var inicio = equipo.FechaRegistro > desde ? equipo.FechaRegistro : desde;
            esperadas += Math.Max(0, (ahora - inicio).TotalMinutes);
        }

        // 2. Rupturas de temperatura del periodo.
        var rupturas = await _db.Alertas.CountAsync(a =>
            a.Equipo.Activo && a.Nivel == NivelAlerta.Ruptura && a.Tipo != TipoAlerta.SinSenal && a.Inicio >= desde);

        // 3. Tiempo de respuesta: de que la alerta exige revisión a que alguien la atiende.
        var atenciones = await _db.Alertas
            .AsNoTracking()
            .Where(a => a.RevisionRequeridaDesde != null && a.FechaAtencion != null
                        && a.FechaAtencion >= a.RevisionRequeridaDesde && a.RevisionRequeridaDesde >= desde)
            .Select(a => new { Desde = a.RevisionRequeridaDesde!.Value, Atencion = a.FechaAtencion!.Value })
            .ToListAsync();

        // 4. Pendientes de revisión, de cualquier fecha.
        var pendientes = await _db.Alertas.CountAsync(a =>
            a.RevisionRequeridaDesde != null && (a.FechaAtencion == null || a.FechaAtencion < a.RevisionRequeridaDesde));

        return Ok(new IndicadoresDto
        {
            Dias = dias,
            PorcentajeOptimo = recibidas > 0 ? Math.Round(100.0 * enOptimo / recibidas, 1) : null,
            Rupturas = rupturas,
            MinutosPromedioRespuesta = atenciones.Count > 0
                ? Math.Round(atenciones.Average(a => (a.Atencion - a.Desde).TotalMinutes), 1)
                : null,
            PorcentajeDisponibilidad = esperadas >= 1 ? Math.Round(Math.Min(100, 100.0 * recibidas / esperadas), 1) : null,
            PendientesDeRevision = pendientes
        });
    }
}
