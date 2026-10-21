using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Lecturas recientes de un equipo, para el mini gráfico y la tendencia del indicador.
[ApiController]
[Authorize]
[Route("api/equipos")]
public class LecturasRecientesController : ControllerBase
{
    // Límite de puntos: con una lectura por minuto, una hora son 60.
    private const int MaximoPuntos = 120;

    private readonly CadenaFrioDbContext _db;

    public LecturasRecientesController(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // GET /api/equipos/{id}/lecturas-recientes?minutos=60
    [HttpGet("{id:int}/lecturas-recientes")]
    public async Task<ActionResult<List<LecturaPuntoDto>>> Obtener(int id, [FromQuery] int minutos = 60)
    {
        var desde = DateTime.UtcNow.AddMinutes(-Math.Clamp(minutos, 5, 24 * 60));

        // Se toman las más recientes y luego se ordenan de la más antigua a la más nueva,
        // que es el orden en que se dibujan en el gráfico.
        var puntos = await _db.Lecturas
            .AsNoTracking()
            .Where(l => l.EquipoId == id && l.FechaMedicion >= desde)
            .OrderByDescending(l => l.FechaMedicion)
            .Take(MaximoPuntos)
            .Select(l => new LecturaPuntoDto { FechaMedicion = l.FechaMedicion, Temperatura = l.Temperatura })
            .ToListAsync();

        puntos.Reverse();
        return Ok(puntos);
    }
}
