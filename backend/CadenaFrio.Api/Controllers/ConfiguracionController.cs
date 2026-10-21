using System.Security.Claims;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Consultas y acciones de la pantalla de Configuración: equipos, catálogo y vacunas por equipo.
[ApiController]
[Authorize]
public class ConfiguracionController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;
    private readonly GestionRangos _rangos;

    public ConfiguracionController(CadenaFrioDbContext db, GestionRangos rangos)
    {
        _db = db;
        _rangos = rangos;
    }

    // GET /api/equipos/lista
    // Equipos activos, para elegir cuál configurar.
    [HttpGet("api/equipos/lista")]
    public async Task<ActionResult<List<EquipoResumenDto>>> ListarEquipos() =>
        Ok(await _db.Equipos
            .AsNoTracking()
            .Where(e => e.Activo)
            .OrderBy(e => e.Nombre)
            .Select(e => new EquipoResumenDto { Id = e.Id, Nombre = e.Nombre, Tipo = (TipoEquipoDto)(int)e.Tipo })
            .ToListAsync());

    // GET /api/vacunas
    // Catálogo de vacunas activas.
    [HttpGet("api/vacunas")]
    public async Task<ActionResult<List<VacunaDto>>> ListarVacunas() =>
        Ok(await _db.Vacunas
            .AsNoTracking()
            .Where(v => v.Activo)
            .OrderBy(v => v.Nombre)
            .Select(v => new VacunaDto
            {
                Id = v.Id,
                Nombre = v.Nombre,
                OptimoMinimo = v.OptimoMinimo,
                OptimoMaximo = v.OptimoMaximo,
                LimiteMinimo = v.LimiteMinimo,
                LimiteMaximo = v.LimiteMaximo,
                Fuente = v.Fuente,
                Activo = v.Activo
            })
            .ToListAsync());

    // GET /api/equipos/{id}/vacunas
    // Identificadores de las vacunas que guarda el equipo.
    [HttpGet("api/equipos/{id:int}/vacunas")]
    public async Task<ActionResult<List<int>>> VacunasDelEquipo(int id) =>
        Ok(await _db.EquipoVacunas.Where(a => a.EquipoId == id).Select(a => a.VacunaId).ToListAsync());

    // PUT /api/equipos/{id}/vacunas
    // Asigna las vacunas y registra el rango calculado. Solo personal autorizado.
    [Authorize(Policy = Politicas.PuedeConfigurarRangos)]
    [HttpPut("api/equipos/{id:int}/vacunas")]
    public async Task<IActionResult> AsignarVacunas(int id, AsignarVacunasRequest solicitud)
    {
        if (!await _db.Equipos.AnyAsync(e => e.Id == id && e.Activo))
        {
            return NotFound(new { mensaje = "Equipo no encontrado o inactivo." });
        }

        var usuario = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;
        var resultado = await _rangos.AsignarVacunasAsync(id, solicitud.VacunaIds, usuario);

        return resultado.Exito
            ? NoContent()
            : Conflict(new { mensaje = resultado.Error });
    }
}
