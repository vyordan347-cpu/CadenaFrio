using CadenaFrio.Api.Reportes;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CadenaFrio.Api.Reportes.EstiloExcel;

namespace CadenaFrio.Api.Controllers;

// Excel de la página de Reportes: solo el periodo y los equipos que se eligieron.
[ApiController]
[Authorize]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private static readonly TimeSpan PeriodoMaximo = TimeSpan.FromDays(92);

    private readonly CadenaFrioDbContext _db;
    private readonly CalculoIndicadores _calculo;

    public ReportesController(CadenaFrioDbContext db, CalculoIndicadores calculo)
    {
        _db = db;
        _calculo = calculo;
    }

    // GET /api/reportes/excel?desde=...&hasta=...&equipoId=1   (fechas en UTC; equipo opcional)
    [HttpGet("excel")]
    public async Task<IActionResult> Excel([FromQuery] DateTime desde, [FromQuery] DateTime hasta, [FromQuery] int? equipoId)
    {
        desde = desde.ToUniversalTime();
        hasta = hasta.ToUniversalTime();
        if (desde >= hasta || hasta - desde > PeriodoMaximo)
        {
            return BadRequest(new { mensaje = "Periodo no válido. Debe ser de como máximo 92 días." });
        }

        string alcance = "Todos los equipos activos";
        if (equipoId is not null)
        {
            var nombre = await _db.Equipos.Where(e => e.Id == equipoId).Select(e => e.Nombre).FirstOrDefaultAsync();
            if (nombre is null)
            {
                return NotFound(new { mensaje = "Equipo no encontrado." });
            }
            alcance = nombre;
        }

        var indicadores = await _calculo.CalcularAsync(desde, hasta, equipoId);
        var equipos = await _calculo.ResumenPorEquipoAsync(desde, hasta, equipoId);
        var ids = equipos.Select(e => e.EquipoId).ToList();

        // Alertas que se superponen con el periodo (empezaron antes de que termine y terminaron después de que empiece).
        var alertas = await _db.Alertas
            .AsNoTracking()
            .Include(a => a.Equipo)
            .Where(a => ids.Contains(a.EquipoId) && a.Inicio < hasta && (a.Fin == null || a.Fin >= desde))
            .OrderBy(a => a.Inicio)
            .ToListAsync();

        // Cada lectura solo en periodos cortos: un mes de todos los equipos serían decenas de miles de filas.
        List<LecturaReporte>? lecturas = null;
        if (hasta - desde <= ReporteExcel.PeriodoMaximoConLecturas)
        {
            lecturas = new();
            foreach (var equipo in equipos)
            {
                // Cada lectura se clasifica con el rango que regía cuando se tomó.
                var historia = await HistoriaRangos.CargarAsync(_db, equipo.EquipoId);
                var delEquipo = await _db.Lecturas
                    .AsNoTracking()
                    .Where(l => l.EquipoId == equipo.EquipoId && l.FechaMedicion >= desde && l.FechaMedicion < hasta)
                    .OrderBy(l => l.FechaMedicion)
                    .Select(l => new { l.FechaMedicion, l.Temperatura })
                    .ToListAsync();

                lecturas.AddRange(delEquipo.Select(l => new LecturaReporte(
                    equipo.Nombre, l.FechaMedicion, l.Temperatura,
                    historia.En(l.FechaMedicion).Clasificar(l.Temperatura).Nivel switch
                    {
                        NivelAlerta.Ruptura => "Ruptura",
                        NivelAlerta.Advertencia => "Advertencia",
                        _ => "Normal"
                    })));
            }
        }

        var archivo = ReporteExcel.Generar(new DatosReporte(desde, hasta, alcance, indicadores, equipos, alertas, lecturas));

        // "reporte-2026-10-20.xlsx" para un día; "reporte-2026-10-14-a-2026-10-20.xlsx" para un periodo.
        // El último día es el anterior a "hasta", porque "hasta" no se incluye.
        var primerDia = Local(desde).Date;
        var ultimoDia = Local(hasta).AddTicks(-1).Date;
        var fechas = primerDia == ultimoDia ? $"{primerDia:yyyy-MM-dd}" : $"{primerDia:yyyy-MM-dd}-a-{ultimoDia:yyyy-MM-dd}";
        var sufijo = equipoId is null ? "" : $"-{Archivo(alcance)}";

        return File(archivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"reporte-{fechas}{sufijo}.xlsx");
    }
}
