using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Gestión de equipos desde el panel de administrador (RF19).
// Toda la clase exige el rol Administrador: los demás reciben 403.
[ApiController]
[Authorize(Policy = Politicas.EsAdministrador)]
[Route("api/admin/equipos")]
public class AdminEquiposController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;

    public AdminEquiposController(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // GET /api/admin/equipos
    [HttpGet]
    public async Task<ActionResult<List<EquipoAdminDto>>> Listar()
    {
        var equipos = await _db.Equipos
            .AsNoTracking()
            .OrderByDescending(e => e.Activo)
            .ThenBy(e => e.Nombre)
            .Select(e => new EquipoAdminDto
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Tipo = (TipoEquipoDto)(int)e.Tipo,
                Ubicacion = e.Ubicacion,
                Activo = e.Activo,
                FechaRegistro = e.FechaRegistro,
                // Subconsulta: la recepción más reciente de cada equipo.
                UltimaSenal = e.Lecturas
                    .OrderByDescending(l => l.FechaRecepcion)
                    .Select(l => (DateTime?)l.FechaRecepcion)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(equipos);
    }

    // POST /api/admin/equipos
    // Registra un equipo y genera su clave. La clave se devuelve una sola vez.
    [HttpPost]
    public async Task<ActionResult<ClaveGeneradaDto>> Registrar(GuardarEquipoRequest solicitud)
    {
        var clave = ClavesDispositivo.Generar();

        var equipo = new Equipo
        {
            Nombre = solicitud.Nombre!.Trim(),
            Tipo = (TipoEquipo)(int)solicitud.Tipo!.Value,
            Ubicacion = solicitud.Ubicacion?.Trim() ?? string.Empty,
            ClaveDispositivo = ClavesDispositivo.Hash(clave),
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };

        _db.Equipos.Add(equipo);
        await _db.SaveChangesAsync();

        return Ok(new ClaveGeneradaDto { Equipo = ConvertirADto(equipo, ultimaSenal: null), Clave = clave });
    }

    // PUT /api/admin/equipos/{id}
    // Edita nombre, tipo y ubicación. La clave no cambia.
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EquipoAdminDto>> Editar(int id, GuardarEquipoRequest solicitud)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        equipo.Nombre = solicitud.Nombre!.Trim();
        equipo.Tipo = (TipoEquipo)(int)solicitud.Tipo!.Value;
        equipo.Ubicacion = solicitud.Ubicacion?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync();

        return Ok(ConvertirADto(equipo, await UltimaSenalAsync(id)));
    }

    // POST /api/admin/equipos/{id}/regenerar-clave
    // Reemplaza la clave: la anterior deja de funcionar al instante.
    [HttpPost("{id:int}/regenerar-clave")]
    public async Task<ActionResult<ClaveGeneradaDto>> RegenerarClave(int id)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        var clave = ClavesDispositivo.Generar();
        equipo.ClaveDispositivo = ClavesDispositivo.Hash(clave);
        await _db.SaveChangesAsync();

        return Ok(new ClaveGeneradaDto { Equipo = ConvertirADto(equipo, await UltimaSenalAsync(id)), Clave = clave });
    }

    // POST /api/admin/equipos/{id}/desactivar
    // El equipo deja de aceptar lecturas y el vigilante lo ignora. Su historial se conserva.
    [HttpPost("{id:int}/desactivar")]
    public async Task<ActionResult<EquipoAdminDto>> Desactivar(int id)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        equipo.Activo = false;

        // Sus alertas abiertas se cierran: un equipo fuera de servicio no debe seguir
        // mostrando alertas "en curso" para siempre. Las que exigen revisión siguen pendientes.
        var ahora = DateTime.UtcNow;
        var abiertas = await _db.Alertas.Where(a => a.EquipoId == id && a.Fin == null).ToListAsync();
        foreach (var alerta in abiertas)
        {
            alerta.Fin = ahora;
        }

        await _db.SaveChangesAsync();
        return Ok(ConvertirADto(equipo, await UltimaSenalAsync(id)));
    }

    // POST /api/admin/equipos/{id}/activar
    [HttpPost("{id:int}/activar")]
    public async Task<ActionResult<EquipoAdminDto>> Activar(int id)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        equipo.Activo = true;
        await _db.SaveChangesAsync();
        return Ok(ConvertirADto(equipo, await UltimaSenalAsync(id)));
    }

    private Task<DateTime?> UltimaSenalAsync(int equipoId) =>
        _db.Lecturas
            .Where(l => l.EquipoId == equipoId)
            .OrderByDescending(l => l.FechaRecepcion)
            .Select(l => (DateTime?)l.FechaRecepcion)
            .FirstOrDefaultAsync();

    private static EquipoAdminDto ConvertirADto(Equipo e, DateTime? ultimaSenal) => new()
    {
        Id = e.Id,
        Nombre = e.Nombre,
        Tipo = (TipoEquipoDto)(int)e.Tipo,
        Ubicacion = e.Ubicacion,
        Activo = e.Activo,
        FechaRegistro = e.FechaRegistro,
        UltimaSenal = ultimaSenal
    };
}
