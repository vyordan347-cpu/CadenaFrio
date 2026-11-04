using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CadenaFrio.Api.Controllers;

// Indicadores clave: miden de verdad las métricas que promete el documento.
// El cálculo está en CalculoIndicadores, compartido con el Excel de Reportes.
[ApiController]
[Authorize]
[Route("api/indicadores")]
public class IndicadoresController : ControllerBase
{
    private static readonly TimeSpan PeriodoMaximo = TimeSpan.FromDays(92);

    private readonly CalculoIndicadores _calculo;

    public IndicadoresController(CalculoIndicadores calculo)
    {
        _calculo = calculo;
    }

    // GET /api/indicadores?desde=...&hasta=...&equipoId=1   (fechas en UTC; equipo opcional)
    // GET /api/indicadores?dias=7                            (los últimos N días, como antes)
    [HttpGet]
    public async Task<ActionResult<IndicadoresDto>> Obtener(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int? equipoId, [FromQuery] int dias = 7)
    {
        var ahora = DateTime.UtcNow;
        DateTime inicio, fin;

        if (desde is not null && hasta is not null)
        {
            (inicio, fin) = (desde.Value.ToUniversalTime(), hasta.Value.ToUniversalTime());
            if (inicio >= fin || fin - inicio > PeriodoMaximo)
            {
                return BadRequest(new { mensaje = "Periodo no válido. Debe ser de como máximo 92 días." });
            }
        }
        else
        {
            dias = Math.Clamp(dias, 1, 90);
            (inicio, fin) = (ahora.AddDays(-dias), ahora);
        }

        var r = await _calculo.CalcularAsync(inicio, fin, equipoId, ahora);

        return Ok(new IndicadoresDto
        {
            Dias = (int)Math.Ceiling((fin - inicio).TotalDays),
            Desde = inicio,
            Hasta = fin,
            PorcentajeOptimo = r.PorcentajeOptimo,
            Rupturas = r.Rupturas,
            MinutosPromedioRespuesta = r.MinutosPromedioRespuesta,
            PorcentajeDisponibilidad = r.PorcentajeDisponibilidad,
            PendientesDeRevision = r.PendientesDeRevision
        });
    }
}
