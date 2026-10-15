using CadenaFrio.Domain.Entities;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
 
namespace CadenaFrio.Api.Controllers;
 
[ApiController]
[Route("api/equipos")]
public class EquiposController : ControllerBase
{
    // Si la última lectura es más antigua que esto, se considera desactualizada.
    // El dispositivo envía una lectura por minuto, así que 5 minutos sin datos ya es anormal.
    private static readonly TimeSpan LimiteActualizacion = TimeSpan.FromMinutes(5);
 
    private readonly CadenaFrioDbContext _db;
 
    public EquiposController(CadenaFrioDbContext db)
    {
        _db = db;
    }
 
    // GET /api/equipos/{id}/temperatura-actual  (HU01, RF02)
    [HttpGet("{id:int}/temperatura-actual")]
    public async Task<ActionResult<TemperaturaActualDto>> ObtenerTemperaturaActual(int id)
    {
        // AsNoTracking: solo leemos, así que EF no necesita vigilar cambios (más rápido).
        var equipo = await _db.Equipos
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
 
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }
 
        // La lectura más reciente según cuándo se midió, no cuándo llegó:
        // una lectura sincronizada tarde no debe pasar por "la última".
        var ultimaLectura = await _db.Lecturas
            .AsNoTracking()
            .Where(l => l.EquipoId == id)
            .OrderByDescending(l => l.FechaMedicion)
            .FirstOrDefaultAsync();
 
        var respuesta = new TemperaturaActualDto
        {
            EquipoId = equipo.Id,
            NombreEquipo = equipo.Nombre
        };
 
        if (ultimaLectura is null)
        {
            respuesta.Estado = EstadoLectura.SinDatos;
            return Ok(respuesta);
        }
 
        respuesta.Temperatura = ultimaLectura.Temperatura;
        respuesta.FechaMedicion = ultimaLectura.FechaMedicion;
        respuesta.Estado = DateTime.UtcNow - ultimaLectura.FechaMedicion > LimiteActualizacion
            ? EstadoLectura.Desactualizada
            : EstadoLectura.Actualizada;
 
        return Ok(respuesta);
    }
 
    // GET /api/equipos/{id}/rango  (HU02)
    // Devuelve el rango vigente, o el de por defecto si nunca se configuró.
    [HttpGet("{id:int}/rango")]
    public async Task<ActionResult<RangoTemperaturaDto>> ObtenerRango(int id)
    {
        if (!await _db.Equipos.AnyAsync(e => e.Id == id))
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }
 
        var rango = await ObtenerRangoVigenteAsync(id);
 
        return Ok(rango is null ? RangoPorDefecto(id) : ConvertirADto(rango));
    }
 
    // GET /api/equipos/{id}/rangos  (HU02, RF05)
    // Historial de cambios de rango, del más reciente al más antiguo.
    [HttpGet("{id:int}/rangos")]
    public async Task<ActionResult<List<RangoTemperaturaDto>>> ObtenerHistorialRangos(int id)
    {
        if (!await _db.Equipos.AnyAsync(e => e.Id == id))
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }
 
        var historial = await _db.RangosTemperatura
            .AsNoTracking()
            .Where(r => r.EquipoId == id)
            .OrderByDescending(r => r.FechaCambio)
            .ThenByDescending(r => r.Id)
            .ToListAsync();
 
        return Ok(historial.Select(ConvertirADto).ToList());
    }
 
    // POST /api/equipos/{id}/rango  (HU02, RF04, RF05)
    // Registra un rango nuevo. Nunca modifica uno existente: cada cambio queda en el historial.
    // Las validaciones del DTO (rango físico, mínimo < máximo, nombre) ya se aplicaron
    // automáticamente gracias a [ApiController].
    [HttpPost("{id:int}/rango")]
    public async Task<ActionResult<RangoTemperaturaDto>> ConfigurarRango(int id, ConfigurarRangoRequest solicitud)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
 
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }
 
        if (!equipo.Activo)
        {
            return BadRequest(new { mensaje = "No se puede configurar el rango de un equipo inactivo." });
        }
 
        var minima = solicitud.TemperaturaMinima!.Value;
        var maxima = solicitud.TemperaturaMaxima!.Value;
 
        // Si el rango es igual al vigente, no se registra nada: el historial
        // solo debe contener cambios reales.
        // 409 Conflict indica que la solicitud no tiene sentido con el estado actual.
        var vigente = await ObtenerRangoVigenteAsync(id);
        if (vigente is not null && vigente.TemperaturaMinima == minima && vigente.TemperaturaMaxima == maxima)
        {
            return Conflict(new { mensaje = "El rango indicado es igual al vigente. No se registró ningún cambio." });
        }
 
        var rango = new RangoTemperatura
        {
            EquipoId = id,
            TemperaturaMinima = minima,
            TemperaturaMaxima = maxima,
            ModificadoPor = solicitud.ModificadoPor!.Trim(),
            FechaCambio = DateTime.UtcNow
        };
 
        _db.RangosTemperatura.Add(rango);
        await _db.SaveChangesAsync();
 
        // 201 Created: indica que se creó un recurso nuevo y dónde consultarlo.
        return CreatedAtAction(nameof(ObtenerRango), new { id }, ConvertirADto(rango));
    }
 
    // El vigente es el más reciente. El Id desempata si dos cambios
    // tuvieran exactamente la misma fecha.
    private Task<RangoTemperatura?> ObtenerRangoVigenteAsync(int equipoId) =>
        _db.RangosTemperatura
            .AsNoTracking()
            .Where(r => r.EquipoId == equipoId)
            .OrderByDescending(r => r.FechaCambio)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync();
 
    private static RangoTemperaturaDto ConvertirADto(RangoTemperatura rango) => new()
    {
        EquipoId = rango.EquipoId,
        TemperaturaMinima = rango.TemperaturaMinima,
        TemperaturaMaxima = rango.TemperaturaMaxima,
        FechaCambio = rango.FechaCambio,
        ModificadoPor = rango.ModificadoPor,
        EsPorDefecto = false
    };
 
    private static RangoTemperaturaDto RangoPorDefecto(int equipoId) => new()
    {
        EquipoId = equipoId,
        TemperaturaMinima = RangoTemperatura.MinimaPorDefecto,
        TemperaturaMaxima = RangoTemperatura.MaximaPorDefecto,
        EsPorDefecto = true
    };
}