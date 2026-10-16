using System.Security.Claims;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Revisión de alertas por el personal (RF08).
// Cualquier usuario con sesión puede atenderlas: la enfermera suele ser quien está presente.
[ApiController]
[Authorize]
[Route("api/alertas")]
public class AlertasController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;

    public AlertasController(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // GET /api/alertas/pendientes?equipoId=1
    // Alertas que exigen revisión y no la tienen: nunca atendidas, o atendidas antes de
    // que la exigieran. Incluye rupturas y silencios prolongados, y también las que ya
    // terminaron: que la situación se normalice no significa que alguien lo vio.
    [HttpGet("pendientes")]
    public async Task<ActionResult<List<AlertaPendienteDto>>> ObtenerPendientes([FromQuery] int equipoId)
    {
        var pendientes = await _db.Alertas
            .AsNoTracking()
            .Where(a => a.EquipoId == equipoId
                        && a.RevisionRequeridaDesde != null
                        && (a.FechaAtencion == null || a.FechaAtencion < a.RevisionRequeridaDesde))
            .OrderBy(a => a.Inicio)
            .Select(a => new AlertaPendienteDto
            {
                Id = a.Id,
                Tipo = (TipoAlertaDto)(int)a.Tipo,
                Inicio = a.Inicio,
                FechaRuptura = a.FechaRuptura,
                Fin = a.Fin,
                DetectadaTarde = a.DetectadaTarde,
                ValorExtremo = a.ValorExtremo,
                LimiteMinimo = a.LimiteMinimo,
                LimiteMaximo = a.LimiteMaximo
            })
            .ToListAsync();

        return Ok(pendientes);
    }

    // POST /api/alertas/{id}/atender
    // Registra quién revisó la alerta, cuándo y qué hizo.
    [HttpPost("{id:int}/atender")]
    public async Task<IActionResult> Atender(int id, AtenderAlertaRequest solicitud)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == id);

        if (alerta is null)
        {
            return NotFound(new { mensaje = "Alerta no encontrada." });
        }

        // Una alerta atendida después de que exigiera revisión no necesita otra.
        var yaAtendida = alerta.FechaAtencion is not null
                         && (alerta.RevisionRequeridaDesde is null || alerta.FechaAtencion >= alerta.RevisionRequeridaDesde);
        if (yaAtendida)
        {
            return Conflict(new { mensaje = $"Esta alerta ya fue atendida por {alerta.AtendidaPor}." });
        }

        alerta.AtendidaPor = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;
        alerta.FechaAtencion = DateTime.UtcNow;
        alerta.ObservacionAtencion = solicitud.Observacion!.Trim();

        await _db.SaveChangesAsync();

        // 204 No Content: se realizó la acción y no hay nada que devolver.
        return NoContent();
    }
}
