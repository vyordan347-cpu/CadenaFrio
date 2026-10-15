using System.Security.Claims;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// [Authorize] en la clase: todos estos endpoints requieren sesión iniciada.
// (Las lecturas del ESP32 no pasan por aquí: usan su clave de dispositivo.)
[ApiController]
[Authorize]
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

    // GET /api/equipos/{id}/temperatura-actual  (HU01, HU03, RF02)
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

        var limites = await _db.ObtenerLimitesAsync(id);

        var respuesta = new TemperaturaActualDto
        {
            EquipoId = equipo.Id,
            NombreEquipo = equipo.Nombre,
            OptimoMinimo = limites.OptimoMinimo,
            OptimoMaximo = limites.OptimoMaximo,
            LimiteMinimo = limites.LimiteMinimo,
            LimiteMaximo = limites.LimiteMaximo
        };

        // La lectura más reciente según cuándo se midió, no cuándo llegó:
        // una lectura sincronizada tarde no debe pasar por "la última".
        var ultimaLectura = await _db.Lecturas
            .AsNoTracking()
            .Where(l => l.EquipoId == id)
            .OrderByDescending(l => l.FechaMedicion)
            .FirstOrDefaultAsync();

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

        // Alerta de temperatura activa, si la hay (HU03).
        var alerta = await _db.ObtenerAlertaTemperaturaAbiertaAsync(id);
        if (alerta is not null)
        {
            respuesta.AlertaActiva = new AlertaActivaDto
            {
                Tipo = (TipoAlertaDto)(int)alerta.Tipo,
                Nivel = (NivelAlertaDto)(int)alerta.Nivel,
                Inicio = alerta.Inicio,
                FechaRuptura = alerta.FechaRuptura,
                ValorExtremo = alerta.ValorExtremo
            };
        }

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

        var rango = await _db.ObtenerRangoVigenteAsync(id);

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
    // Las validaciones del DTO (rango físico y relación entre óptimo y límite) ya se aplicaron
    // automáticamente gracias a [ApiController].
    // Solo el personal autorizado (responsable o administrador) puede usarlo: los demás reciben 403.
    [Authorize(Policy = Politicas.PuedeConfigurarRangos)]
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

        var nuevos = new LimitesTemperatura(
            solicitud.OptimoMinimo!.Value,
            solicitud.OptimoMaximo!.Value,
            solicitud.LimiteMinimo!.Value,
            solicitud.LimiteMaximo!.Value);

        // Si los rangos son iguales a los vigentes, no se registra nada: el historial
        // solo debe contener cambios reales. Al ser un record, LimitesTemperatura
        // se compara por sus valores, no por referencia.
        // 409 Conflict indica que la solicitud no tiene sentido con el estado actual.
        var vigente = await _db.ObtenerRangoVigenteAsync(id);
        if (vigente is not null
            && new LimitesTemperatura(vigente.OptimoMinimo, vigente.OptimoMaximo, vigente.LimiteMinimo, vigente.LimiteMaximo) == nuevos)
        {
            return Conflict(new { mensaje = "Los rangos indicados son iguales a los vigentes. No se registró ningún cambio." });
        }

        var rango = new RangoTemperatura
        {
            EquipoId = id,
            OptimoMinimo = nuevos.OptimoMinimo,
            OptimoMaximo = nuevos.OptimoMaximo,
            LimiteMinimo = nuevos.LimiteMinimo,
            LimiteMaximo = nuevos.LimiteMaximo,
            // Quién hizo el cambio se toma de la sesión, no de lo que envíe la pantalla.
            ModificadoPor = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!,
            FechaCambio = DateTime.UtcNow
        };

        _db.RangosTemperatura.Add(rango);
        await _db.SaveChangesAsync();

        // 201 Created: indica que se creó un recurso nuevo y dónde consultarlo.
        return CreatedAtAction(nameof(ObtenerRango), new { id }, ConvertirADto(rango));
    }

    private static RangoTemperaturaDto ConvertirADto(RangoTemperatura rango) => new()
    {
        EquipoId = rango.EquipoId,
        OptimoMinimo = rango.OptimoMinimo,
        OptimoMaximo = rango.OptimoMaximo,
        LimiteMinimo = rango.LimiteMinimo,
        LimiteMaximo = rango.LimiteMaximo,
        FechaCambio = rango.FechaCambio,
        ModificadoPor = rango.ModificadoPor,
        EsPorDefecto = false
    };

    private static RangoTemperaturaDto RangoPorDefecto(int equipoId) => new()
    {
        EquipoId = equipoId,
        OptimoMinimo = LimitesTemperatura.PorDefecto.OptimoMinimo,
        OptimoMaximo = LimitesTemperatura.PorDefecto.OptimoMaximo,
        LimiteMinimo = LimitesTemperatura.PorDefecto.LimiteMinimo,
        LimiteMaximo = LimitesTemperatura.PorDefecto.LimiteMaximo,
        EsPorDefecto = true
    };
}
