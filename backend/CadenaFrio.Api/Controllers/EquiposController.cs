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
}