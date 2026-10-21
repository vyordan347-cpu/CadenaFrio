using System.Security.Claims;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Catálogo de vacunas, gestionado por el administrador.
[ApiController]
[Authorize(Policy = Politicas.EsAdministrador)]
[Route("api/admin/vacunas")]
public class AdminVacunasController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;
    private readonly GestionRangos _rangos;

    public AdminVacunasController(CadenaFrioDbContext db, GestionRangos rangos)
    {
        _db = db;
        _rangos = rangos;
    }

    private string UsuarioActual =>
        User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;

    // GET /api/admin/vacunas
    [HttpGet]
    public async Task<ActionResult<List<VacunaDto>>> Listar()
    {
        var vacunas = await _db.Vacunas
            .AsNoTracking()
            .OrderByDescending(v => v.Activo)
            .ThenBy(v => v.Nombre)
            .Select(v => new VacunaDto
            {
                Id = v.Id,
                Nombre = v.Nombre,
                OptimoMinimo = v.OptimoMinimo,
                OptimoMaximo = v.OptimoMaximo,
                LimiteMinimo = v.LimiteMinimo,
                LimiteMaximo = v.LimiteMaximo,
                Fuente = v.Fuente,
                Activo = v.Activo,
                CantidadEquipos = v.Equipos.Count
            })
            .ToListAsync();

        return Ok(vacunas);
    }

    // POST /api/admin/vacunas
    [HttpPost]
    public async Task<IActionResult> Crear(GuardarVacunaRequest solicitud)
    {
        var nombre = solicitud.Nombre!.Trim();
        if (await _db.Vacunas.AnyAsync(v => v.Nombre == nombre))
        {
            return Conflict(new { mensaje = $"Ya existe la vacuna \"{nombre}\"." });
        }

        var vacuna = new Vacuna { Nombre = nombre, Activo = true };
        Aplicar(vacuna, solicitud);
        _db.Vacunas.Add(vacuna);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PUT /api/admin/vacunas/{id}
    // Si cambian los rangos, se recalculan los equipos que la guardan.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, GuardarVacunaRequest solicitud)
    {
        var vacuna = await _db.Vacunas.FirstOrDefaultAsync(v => v.Id == id);
        if (vacuna is null)
        {
            return NotFound(new { mensaje = "Vacuna no encontrada." });
        }

        var nombre = solicitud.Nombre!.Trim();
        if (await _db.Vacunas.AnyAsync(v => v.Nombre == nombre && v.Id != id))
        {
            return Conflict(new { mensaje = $"Ya existe la vacuna \"{nombre}\"." });
        }

        // Antes de guardar, se verifica que ningún equipo quede sin rango común.
        var nuevos = new LimitesTemperatura(solicitud.OptimoMinimo!.Value, solicitud.OptimoMaximo!.Value,
            solicitud.LimiteMinimo!.Value, solicitud.LimiteMaximo!.Value);
        if (await _rangos.ValidarCambioDeVacunaAsync(id, nuevos) is { } problema)
        {
            return Conflict(new { mensaje = problema });
        }

        vacuna.Nombre = nombre;
        Aplicar(vacuna, solicitud);
        await _db.SaveChangesAsync();

        await _rangos.RecalcularEquiposDeVacunaAsync(id, UsuarioActual);
        return NoContent();
    }

    // POST /api/admin/vacunas/{id}/desactivar
    // Solo si ningún equipo la guarda: retirarla cambiaría sus rangos sin que nadie lo decida.
    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var vacuna = await _db.Vacunas.Include(v => v.Equipos).FirstOrDefaultAsync(v => v.Id == id);
        if (vacuna is null)
        {
            return NotFound(new { mensaje = "Vacuna no encontrada." });
        }

        if (vacuna.Equipos.Count > 0)
        {
            return Conflict(new { mensaje = $"La guardan {vacuna.Equipos.Count} equipo(s). Quítela de esos equipos en Configuración antes de retirarla." });
        }

        vacuna.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/admin/vacunas/{id}/activar
    [HttpPost("{id:int}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        var vacuna = await _db.Vacunas.FirstOrDefaultAsync(v => v.Id == id);
        if (vacuna is null)
        {
            return NotFound(new { mensaje = "Vacuna no encontrada." });
        }

        vacuna.Activo = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Aplicar(Vacuna vacuna, GuardarVacunaRequest s)
    {
        vacuna.OptimoMinimo = s.OptimoMinimo!.Value;
        vacuna.OptimoMaximo = s.OptimoMaximo!.Value;
        vacuna.LimiteMinimo = s.LimiteMinimo!.Value;
        vacuna.LimiteMaximo = s.LimiteMaximo!.Value;
        vacuna.Fuente = s.Fuente?.Trim() ?? string.Empty;
    }
}
