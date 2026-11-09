using ClosedXML.Excel;
using static CadenaFrio.Api.Reportes.EstiloExcel;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Historial y reportes (HU07, RF14, RF15, RF16).
// El archivo de Excel se genera con ClosedXML, una librería gratuita (licencia MIT).
[ApiController]
[Authorize]
[Route("api/historial")]
public class HistorialController : ControllerBase
{
    // Cantidad aproximada de puntos del gráfico, sin importar la longitud del periodo.
    private const int PuntosObjetivo = 300;
    private static readonly TimeSpan PeriodoMaximo = TimeSpan.FromDays(92);
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    private readonly CadenaFrioDbContext _db;

    public HistorialController(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // GET /api/historial?equipoId=1&desde=...&hasta=...   (fechas en UTC)
    [HttpGet]
    public async Task<ActionResult<HistorialDto>> Obtener([FromQuery] int equipoId, [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var periodo = ValidarPeriodo(desde, hasta);
        if (periodo is null)
        {
            return BadRequest(new { mensaje = "Periodo no válido. Debe ser de como máximo 92 días." });
        }
        (desde, hasta) = periodo.Value;

        var equipo = await _db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == equipoId);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        // Cada lectura se juzga con el rango que regía cuando se tomó (no con el de hoy).
        var historia = await HistoriaRangos.CargarAsync(_db, equipoId);
        var tramos = historia.Tramos(desde, hasta);
        var limites = tramos[^1].Limites;   // el que regía al final del periodo
        var lecturas = await LecturasAsync(equipoId, desde, hasta);
        var alertas = await AlertasAsync(equipoId, desde, hasta);

        var resultado = new HistorialDto
        {
            EquipoId = equipo.Id,
            NombreEquipo = equipo.Nombre,
            Desde = desde,
            Hasta = hasta,
            OptimoMinimo = limites.OptimoMinimo,
            OptimoMaximo = limites.OptimoMaximo,
            LimiteMinimo = limites.LimiteMinimo,
            LimiteMaximo = limites.LimiteMaximo,
            Rangos = tramos.Select(t => new TramoRangoDto
            {
                Desde = t.Desde,
                Hasta = t.Hasta,
                OptimoMinimo = t.Limites.OptimoMinimo,
                OptimoMaximo = t.Limites.OptimoMaximo,
                LimiteMinimo = t.Limites.LimiteMinimo,
                LimiteMaximo = t.Limites.LimiteMaximo
            }).ToList(),
            Puntos = Resumir(lecturas, desde, hasta),
            Alertas = alertas.Select(ConvertirAlerta).ToList()
        };

        // Estadísticas del periodo.
        var e = resultado.Estadisticas;
        e.CantidadLecturas = lecturas.Count;
        if (lecturas.Count > 0)
        {
            e.Minima = lecturas.Min(l => l.Temperatura);
            e.Maxima = lecturas.Max(l => l.Temperatura);
            e.Promedio = Math.Round(lecturas.Average(l => l.Temperatura), 2);
            // Proporción de LECTURAS (no de tiempo: si faltan datos, no equivalen) dentro del
            // óptimo, y lecturas fuera del límite, cada una con el rango de su momento.
            var conteo = historia.Contar(lecturas.Select(l => (l.FechaMedicion, l.Temperatura)));
            e.PorcentajeOptimo = Math.Round(100.0 * conteo.EnOptimo / lecturas.Count, 1);
            e.CantidadFueraDelLimite = conteo.FueraDelLimite;
        }

        e.CantidadAlertas = alertas.Count;
        e.CantidadRupturas = alertas.Count(a => a.Nivel == NivelAlerta.Ruptura && a.Tipo != TipoAlerta.SinSenal);
        e.MinutosEnRuptura = Math.Round(alertas
            .Where(a => a.FechaRuptura is not null && a.Tipo != TipoAlerta.SinSenal)
            .Sum(a => MinutosDentro(a.FechaRuptura!.Value, a.Fin ?? DateTime.UtcNow, desde, hasta)), 1);

        return Ok(resultado);
    }

    // GET /api/historial/excel?equipoId=1&desde=...&hasta=...  (RF16)
    // Archivo de Excel real (.xlsx) con tres hojas: Resumen, Lecturas y Alertas.
    // A diferencia de un CSV, se ve igual en cualquier computadora y conserva formato y colores.
    [HttpGet("excel")]
    public async Task<IActionResult> ExportarExcel([FromQuery] int equipoId, [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var respuesta = await Obtener(equipoId, desde, hasta);
        if (respuesta.Result is not OkObjectResult { Value: HistorialDto historial })
        {
            return respuesta.Result!;
        }

        var lecturas = await _db.Lecturas
            .AsNoTracking()
            .Where(l => l.EquipoId == equipoId && l.FechaMedicion >= historial.Desde && l.FechaMedicion <= historial.Hasta)
            .OrderBy(l => l.FechaMedicion)
            .Select(l => new { l.FechaMedicion, l.Temperatura, l.FechaRecepcion })
            .ToListAsync();

        var alertas = await AlertasAsync(equipoId, historial.Desde, historial.Hasta);

        // Cada lectura se clasifica con el rango que regía cuando se tomó.
        var historia = await HistoriaRangos.CargarAsync(_db, equipoId);

        using var libro = new XLWorkbook();

        // ---- Hoja 1: Resumen ----
        var resumen = libro.Worksheets.Add("Resumen");
        resumen.Cell("A1").Value = "Reporte de cadena de frío";
        resumen.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(AzulMarino);
        resumen.Cell("A2").Value = "Sistema IoT de monitoreo de vacunas";
        resumen.Cell("A2").Style.Font.SetFontColor(XLColor.Gray);

        var e = historial.Estadisticas;
        var filas = new List<(string Etiqueta, XLCellValue Valor, string? Formato)>
        {
            ("Equipo", (XLCellValue)historial.NombreEquipo, null),
            ("Desde", Local(historial.Desde), FormatoFecha),
            ("Hasta", Local(historial.Hasta), FormatoFecha)
        };

        // Rangos del periodo. Si cambió, uno por fila con la fecha desde la que rigió, y una nota:
        // cada lectura se juzga con el rango de su momento.
        if (historial.Rangos.Count <= 1)
        {
            filas.Add(("Rango óptimo (°C)", $"{historial.OptimoMinimo:0.0} a {historial.OptimoMaximo:0.0}", null));
            filas.Add(("Rango límite (°C)", $"{historial.LimiteMinimo:0.0} a {historial.LimiteMaximo:0.0}", null));
        }
        else
        {
            foreach (var tramo in historial.Rangos)
            {
                filas.Add(($"Rango desde {Local(tramo.Desde):dd/MM/yyyy HH:mm}",
                    $"óptimo {tramo.OptimoMinimo:0.0} a {tramo.OptimoMaximo:0.0} °C; límite {tramo.LimiteMinimo:0.0} a {tramo.LimiteMaximo:0.0} °C", null));
            }
            filas.Add(("Nota", "El rango cambió en el periodo: cada lectura se juzga con el rango que regía cuando se tomó.", null));
        }

        filas.AddRange(new (string, XLCellValue, string?)[]
        {
            ("Lecturas", (double)e.CantidadLecturas, "#,##0"),
            // Proporción de lecturas, no de tiempo: si faltan datos no equivalen.
            ("Lecturas en rango óptimo", e.PorcentajeOptimo is { } p ? (XLCellValue)(p / 100) : Blank.Value, "0.0%"),
            ("Lecturas fuera del límite", (double)e.CantidadFueraDelLimite, "#,##0"),
            ("Temperatura mínima (°C)", e.Minima is { } mn ? (XLCellValue)mn : Blank.Value, "0.0"),
            ("Temperatura máxima (°C)", e.Maxima is { } mx ? (XLCellValue)mx : Blank.Value, "0.0"),
            ("Temperatura promedio (°C)", e.Promedio is { } pr ? (XLCellValue)pr : Blank.Value, "0.00"),
            ("Alertas", (double)e.CantidadAlertas, "0"),
            ("Rupturas", (double)e.CantidadRupturas, "0"),
            // Sale de las alertas abiertas, no de lecturas continuas (puede incluir tiempo sin datos).
            ("Tiempo con alertas de ruptura abiertas (min)", e.MinutosEnRuptura, "#,##0"),
            ("Generado", Local(DateTime.UtcNow), FormatoFecha)
        });

        for (var i = 0; i < filas.Count; i++)
        {
            var fila = 4 + i;
            resumen.Cell(fila, 1).Value = filas[i].Etiqueta;
            resumen.Cell(fila, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#52606B"));
            resumen.Cell(fila, 2).Value = filas[i].Valor;
            resumen.Cell(fila, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            if (filas[i].Formato is { } formato)
            {
                resumen.Cell(fila, 2).Style.NumberFormat.Format = formato;
            }
        }
        resumen.Column(1).Width = 40;
        resumen.Column(2).Width = 56;
        resumen.Column(2).Style.Alignment.SetWrapText(true);

        // ---- Hoja 2: Lecturas ----
        var hojaLecturas = libro.Worksheets.Add("Lecturas");
        Encabezados(hojaLecturas, "Fecha de medición", "Temperatura (°C)", "Estado", "Fecha de recepción");
        for (var i = 0; i < lecturas.Count; i++)
        {
            var l = lecturas[i];
            var fila = i + 2;
            var (estado, color) = Clasificar(l.Temperatura, historia.En(l.FechaMedicion));

            hojaLecturas.Cell(fila, 1).Value = Local(l.FechaMedicion);
            hojaLecturas.Cell(fila, 1).Style.NumberFormat.Format = FormatoFecha;
            hojaLecturas.Cell(fila, 2).Value = l.Temperatura;
            hojaLecturas.Cell(fila, 2).Style.NumberFormat.Format = "0.00";
            hojaLecturas.Cell(fila, 3).Value = estado;
            hojaLecturas.Cell(fila, 4).Value = Local(l.FechaRecepcion);
            hojaLecturas.Cell(fila, 4).Style.NumberFormat.Format = FormatoFecha;

            // Las lecturas fuera de rango se resaltan: rojo en ruptura, ámbar en advertencia.
            if (color is not null)
            {
                hojaLecturas.Range(fila, 1, fila, 4).Style.Fill.SetBackgroundColor(color);
            }
        }
        Terminar(hojaLecturas, lecturas.Count, 4, new[] { 28.0, 18, 16, 30 });

        // ---- Hoja 3: Alertas ----
        var hojaAlertas = libro.Worksheets.Add("Alertas");
        Encabezados(hojaAlertas, "Tipo", "Nivel", "Inicio", "Fin", "Duración (min)",
            "Valor extremo (°C)", "Detectada al sincronizar", "Atendida por", "Fecha de atención", "Observación");
        for (var i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            var fila = i + 2;

            hojaAlertas.Cell(fila, 1).Value = NombreTipo(a.Tipo);
            hojaAlertas.Cell(fila, 2).Value = a.Nivel == NivelAlerta.Ruptura ? "Ruptura" : "Advertencia";
            hojaAlertas.Cell(fila, 3).Value = Local(a.Inicio);
            hojaAlertas.Cell(fila, 4).Value = a.Fin is { } fin ? (XLCellValue)Local(fin) : "En curso";
            hojaAlertas.Cell(fila, 5).Value = Math.Round(((a.Fin ?? DateTime.UtcNow) - a.Inicio).TotalMinutes);
            hojaAlertas.Cell(fila, 6).Value = a.ValorExtremo;
            hojaAlertas.Cell(fila, 7).Value = a.DetectadaTarde ? "Sí" : "No";
            hojaAlertas.Cell(fila, 8).Value = a.AtendidaPor ?? (a.RevisionRequeridaDesde is not null ? "PENDIENTE" : "No requiere");
            hojaAlertas.Cell(fila, 9).Value = a.FechaAtencion is { } fa ? (XLCellValue)Local(fa) : Blank.Value;
            hojaAlertas.Cell(fila, 10).Value = a.ObservacionAtencion ?? "";

            hojaAlertas.Cell(fila, 3).Style.NumberFormat.Format = FormatoFecha;
            hojaAlertas.Cell(fila, 4).Style.NumberFormat.Format = FormatoFecha;
            hojaAlertas.Cell(fila, 9).Style.NumberFormat.Format = FormatoFecha;
            hojaAlertas.Cell(fila, 6).Style.NumberFormat.Format = "0.0";
            hojaAlertas.Cell(fila, 10).Style.Alignment.SetWrapText(true);

            var color = a.Tipo is TipoAlerta.SinSenal or TipoAlerta.SensorSinVariacion ? GrisSuave
                : a.Nivel == NivelAlerta.Ruptura ? RojoSuave : AmbarSuave;
            hojaAlertas.Range(fila, 1, fila, 2).Style.Fill.SetBackgroundColor(color);
            if (a.AtendidaPor is null && a.RevisionRequeridaDesde is not null)
            {
                hojaAlertas.Cell(fila, 8).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#B4443C"));
            }
        }
        Terminar(hojaAlertas, alertas.Count, 10, new[] { 18.0, 13, 22, 22, 15, 18, 14, 24, 26, 50 });

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);

        var nombre = $"cadena-frio-{Archivo(historial.NombreEquipo)}-{Local(historial.Hasta):yyyy-MM-dd}.xlsx";
        return File(memoria.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    // ---------- Formato del Excel ----------
    // Colores, fechas y encabezados vienen de Reportes/EstiloExcel (compartido con Reportes).

    // Misma regla que el resto del sistema (LimitesTemperatura.Clasificar), con el rango de la lectura.
    private static (string Estado, XLColor? Color) Clasificar(double t, LimitesTemperatura limites) =>
        limites.Clasificar(t).Nivel switch
        {
            NivelAlerta.Ruptura => ("Ruptura", RojoSuave),
            NivelAlerta.Advertencia => ("Advertencia", AmbarSuave),
            _ => ("Normal", null)
        };

    // ---------- Consultas ----------

    private sealed record LecturaSimple(DateTime FechaMedicion, double Temperatura);

    private Task<List<LecturaSimple>> LecturasAsync(int equipoId, DateTime desde, DateTime hasta) =>
        _db.Lecturas
            .AsNoTracking()
            .Where(l => l.EquipoId == equipoId && l.FechaMedicion >= desde && l.FechaMedicion <= hasta)
            .OrderBy(l => l.FechaMedicion)
            .Select(l => new LecturaSimple(l.FechaMedicion, l.Temperatura))
            .ToListAsync();

    // Alertas que se superponen con el periodo (empezaron antes de que termine y terminaron después de que empiece).
    private Task<List<Alerta>> AlertasAsync(int equipoId, DateTime desde, DateTime hasta) =>
        _db.Alertas
            .AsNoTracking()
            .Where(a => a.EquipoId == equipoId && a.Inicio <= hasta && (a.Fin == null || a.Fin >= desde))
            .OrderByDescending(a => a.Inicio)
            .ToListAsync();

    // ---------- Cálculos ----------

    // Agrupa las lecturas en tramos de igual duración. Cada tramo guarda promedio, mínimo y máximo.
    private static List<PuntoHistorialDto> Resumir(List<LecturaSimple> lecturas, DateTime desde, DateTime hasta)
    {
        if (lecturas.Count == 0)
        {
            return new();
        }

        var tramo = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMinutes(1).Ticks, (hasta - desde).Ticks / PuntosObjetivo));

        return lecturas
            .GroupBy(l => (l.FechaMedicion - desde).Ticks / tramo.Ticks)
            .OrderBy(g => g.Key)
            .Select(g => new PuntoHistorialDto
            {
                Fecha = desde + TimeSpan.FromTicks(g.Key * tramo.Ticks),
                Promedio = Math.Round(g.Average(l => l.Temperatura), 2),
                Minimo = g.Min(l => l.Temperatura),
                Maximo = g.Max(l => l.Temperatura),
                Cantidad = g.Count()
            })
            .ToList();
    }

    // Minutos de un intervalo que caen dentro del periodo consultado.
    private static double MinutosDentro(DateTime inicio, DateTime fin, DateTime desde, DateTime hasta)
    {
        var a = inicio > desde ? inicio : desde;
        var b = fin < hasta ? fin : hasta;
        return b > a ? (b - a).TotalMinutes : 0;
    }

    private static (DateTime, DateTime)? ValidarPeriodo(DateTime desde, DateTime hasta)
    {
        desde = ConvertirAUtc(desde);
        hasta = ConvertirAUtc(hasta);
        return hasta > desde && hasta - desde <= PeriodoMaximo ? (desde, hasta) : null;
    }

    // ASP.NET convierte las fechas con "Z" de la dirección a hora local del servidor;
    // aquí se vuelven a UTC para comparar con la base de datos.
    private static DateTime ConvertirAUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha,
        DateTimeKind.Local => fecha.ToUniversalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
    };

    private static AlertaHistorialDto ConvertirAlerta(Alerta a) => new()
    {
        Id = a.Id,
        Tipo = (TipoAlertaDto)(int)a.Tipo,
        Nivel = (NivelAlertaDto)(int)a.Nivel,
        Inicio = a.Inicio,
        FechaRuptura = a.FechaRuptura,
        Fin = a.Fin,
        ValorExtremo = a.ValorExtremo,
        DetectadaTarde = a.DetectadaTarde,
        RequiereRevision = a.RevisionRequeridaDesde is not null,
        AtendidaPor = a.AtendidaPor,
        FechaAtencion = a.FechaAtencion,
        ObservacionAtencion = a.ObservacionAtencion,
        TieneAnexo13 = CatalogoAnexo13.Aplica(a.Tipo, a.Nivel) && a.CausaRuptura is not null,
        // Los códigos de causa no se repiten entre las dos listas, salvo "otra", que dice lo mismo.
        CausaRuptura = (CatalogoAnexo13.BuscarCausa(TipoEquipo.Refrigeradora, a.CausaRuptura)
                        ?? CatalogoAnexo13.BuscarCausa(TipoEquipo.Termo, a.CausaRuptura))?.Descripcion
    };
}
