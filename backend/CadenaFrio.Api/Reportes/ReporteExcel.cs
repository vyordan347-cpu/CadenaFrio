using ClosedXML.Excel;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Servicios;
using static CadenaFrio.Api.Reportes.EstiloExcel;

namespace CadenaFrio.Api.Reportes;

// Una lectura en la hoja "Lecturas". Estado: "Normal", "Advertencia" o "Ruptura".
public record LecturaReporte(string Equipo, DateTime FechaMedicion, double Temperatura, string Estado);

// Lo que reúne el controlador para el Excel. Fechas en UTC; el periodo es [Desde, Hasta).
// Lecturas es null cuando el periodo es largo: la hoja no se incluye.
public record DatosReporte(
    DateTime Desde,
    DateTime Hasta,
    string Alcance,
    ResultadoIndicadores Indicadores,
    List<ResumenEquipo> Equipos,
    List<Alerta> Alertas,
    List<LecturaReporte>? Lecturas);

// Excel de la página de Reportes: solo el periodo y los equipos elegidos.
public static class ReporteExcel
{
    // Hasta este largo se incluye cada lectura; más, el archivo sería enorme y nadie lo leería.
    public static readonly TimeSpan PeriodoMaximoConLecturas = TimeSpan.FromDays(7);

    public static byte[] Generar(DatosReporte d)
    {
        using var libro = new XLWorkbook();

        // ---- Hoja 1: Resumen ----
        var hoja = libro.Worksheets.Add("Resumen");
        hoja.Cell("A1").Value = "Reporte de cadena de frío";
        hoja.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(AzulMarino);
        hoja.Cell("A2").Value = "Sistema IoT de monitoreo de vacunas";
        hoja.Cell("A2").Style.Font.SetFontColor(XLColor.Gray);

        var k = d.Indicadores;
        var filas = new (string Etiqueta, XLCellValue Valor, string? Formato)[]
        {
            ("Equipos", (XLCellValue)d.Alcance, null),
            ("Desde", Local(d.Desde), FormatoFecha),
            ("Hasta", Local(d.Hasta), FormatoFecha),
            ("Tiempo en rango óptimo", k.PorcentajeOptimo is { } po ? (XLCellValue)(po / 100) : Blank.Value, "0.0%"),
            ("Rupturas de temperatura", k.Rupturas, "0"),
            ("Tiempo promedio de respuesta (min)", k.MinutosPromedioRespuesta is { } m ? (XLCellValue)m : Blank.Value, "0.0"),
            ("Lecturas recibidas", k.PorcentajeDisponibilidad is { } pd ? (XLCellValue)(pd / 100) : Blank.Value, "0.0%"),
            ("Pendientes de revisión (hoy)", k.PendientesDeRevision, "0"),
            ("Generado", Local(DateTime.UtcNow), FormatoFecha)
        };
        for (var i = 0; i < filas.Length; i++)
        {
            var fila = 4 + i;
            hoja.Cell(fila, 1).Value = filas[i].Etiqueta;
            hoja.Cell(fila, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#52606B"));
            hoja.Cell(fila, 2).Value = filas[i].Valor;
            hoja.Cell(fila, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            if (filas[i].Formato is { } formato)
            {
                hoja.Cell(fila, 2).Style.NumberFormat.Format = formato;
            }
        }

        // Una fila por equipo, debajo de los indicadores.
        var inicioTabla = 4 + filas.Length + 2;
        var titulos = new[] { "Equipo", "Ubicación", "Lecturas", "En óptimo", "Mínima (°C)", "Máxima (°C)", "Promedio (°C)", "Rupturas" };
        for (var c = 0; c < titulos.Length; c++)
        {
            hoja.Cell(inicioTabla, c + 1).Value = titulos[c];
        }
        var cabecera = hoja.Range(inicioTabla, 1, inicioTabla, titulos.Length);
        cabecera.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(AzulMarino);

        for (var i = 0; i < d.Equipos.Count; i++)
        {
            var e = d.Equipos[i];
            var fila = inicioTabla + 1 + i;
            hoja.Cell(fila, 1).Value = e.Nombre;
            hoja.Cell(fila, 2).Value = e.Ubicacion;
            hoja.Cell(fila, 3).Value = e.Lecturas;
            hoja.Cell(fila, 4).Value = e.PorcentajeOptimo is { } p ? (XLCellValue)(p / 100) : Blank.Value;
            hoja.Cell(fila, 5).Value = e.Minima is { } mn ? (XLCellValue)mn : Blank.Value;
            hoja.Cell(fila, 6).Value = e.Maxima is { } mx ? (XLCellValue)mx : Blank.Value;
            hoja.Cell(fila, 7).Value = e.Promedio is { } pr ? (XLCellValue)pr : Blank.Value;
            hoja.Cell(fila, 8).Value = e.Rupturas;
            hoja.Cell(fila, 3).Style.NumberFormat.Format = "#,##0";
            hoja.Cell(fila, 4).Style.NumberFormat.Format = "0.0%";
            hoja.Range(fila, 5, fila, 7).Style.NumberFormat.Format = "0.0";
            if (e.Rupturas > 0)
            {
                hoja.Cell(fila, 8).Style.Fill.SetBackgroundColor(RojoSuave);
            }
        }

        // Si no se incluyen las lecturas, se explica por qué y dónde encontrarlas.
        if (d.Lecturas is null)
        {
            var nota = hoja.Cell(inicioTabla + d.Equipos.Count + 2, 1);
            nota.Value = "Para ver cada lectura, elija un periodo de 7 días o menos, o use el Historial de un equipo.";
            nota.Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        }

        hoja.Column(1).Width = 34;
        hoja.Column(2).Width = 26;
        for (var c = 3; c <= titulos.Length; c++) hoja.Column(c).Width = 14;

        // ---- Hoja 2: Alertas ----
        var hojaAlertas = libro.Worksheets.Add("Alertas");
        Encabezados(hojaAlertas, "Equipo", "Tipo", "Nivel", "Inicio", "Fin", "Duración (min)",
            "Valor extremo (°C)", "Revisión", "Fecha de revisión", "Causa (Anexo 13)", "Observación");
        for (var i = 0; i < d.Alertas.Count; i++)
        {
            var a = d.Alertas[i];
            var fila = i + 2;

            hojaAlertas.Cell(fila, 1).Value = a.Equipo.Nombre;
            hojaAlertas.Cell(fila, 2).Value = NombreTipo(a.Tipo);
            hojaAlertas.Cell(fila, 3).Value = a.Nivel == NivelAlerta.Ruptura ? "Ruptura" : "Advertencia";
            hojaAlertas.Cell(fila, 4).Value = Local(a.Inicio);
            hojaAlertas.Cell(fila, 5).Value = a.Fin is { } fin ? (XLCellValue)Local(fin) : "En curso";
            hojaAlertas.Cell(fila, 6).Value = Math.Round(((a.Fin ?? DateTime.UtcNow) - a.Inicio).TotalMinutes);
            hojaAlertas.Cell(fila, 7).Value = a.ValorExtremo;
            hojaAlertas.Cell(fila, 8).Value = a.AtendidaPor ?? (a.RevisionRequeridaDesde is not null ? "PENDIENTE" : "No requiere");
            hojaAlertas.Cell(fila, 9).Value = a.FechaAtencion is { } fa ? (XLCellValue)Local(fa) : Blank.Value;
            hojaAlertas.Cell(fila, 10).Value = CatalogoAnexo13.BuscarCausa(a.Equipo.Tipo, a.CausaRuptura)?.Descripcion ?? "";
            hojaAlertas.Cell(fila, 11).Value = a.ObservacionAtencion ?? "";

            hojaAlertas.Cell(fila, 4).Style.NumberFormat.Format = FormatoFecha;
            hojaAlertas.Cell(fila, 5).Style.NumberFormat.Format = FormatoFecha;
            hojaAlertas.Cell(fila, 9).Style.NumberFormat.Format = FormatoFecha;
            hojaAlertas.Cell(fila, 7).Style.NumberFormat.Format = "0.0";
            hojaAlertas.Cell(fila, 11).Style.Alignment.SetWrapText(true);

            var color = a.Tipo is TipoAlerta.SinSenal or TipoAlerta.SensorSinVariacion ? GrisSuave
                : a.Nivel == NivelAlerta.Ruptura ? RojoSuave : AmbarSuave;
            hojaAlertas.Range(fila, 2, fila, 3).Style.Fill.SetBackgroundColor(color);
            if (a.AtendidaPor is null && a.RevisionRequeridaDesde is not null)
            {
                hojaAlertas.Cell(fila, 8).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#B4443C"));
            }
        }
        Terminar(hojaAlertas, d.Alertas.Count, 11, new[] { 26.0, 18, 13, 22, 22, 14, 16, 24, 22, 34, 50 });

        // ---- Hoja 3: Lecturas (solo periodos cortos) ----
        if (d.Lecturas is { } lecturas)
        {
            var hojaLecturas = libro.Worksheets.Add("Lecturas");
            Encabezados(hojaLecturas, "Equipo", "Fecha de medición", "Temperatura (°C)", "Estado");
            for (var i = 0; i < lecturas.Count; i++)
            {
                var l = lecturas[i];
                var fila = i + 2;
                hojaLecturas.Cell(fila, 1).Value = l.Equipo;
                hojaLecturas.Cell(fila, 2).Value = Local(l.FechaMedicion);
                hojaLecturas.Cell(fila, 2).Style.NumberFormat.Format = FormatoFecha;
                hojaLecturas.Cell(fila, 3).Value = l.Temperatura;
                hojaLecturas.Cell(fila, 3).Style.NumberFormat.Format = "0.00";
                hojaLecturas.Cell(fila, 4).Value = l.Estado;

                if (l.Estado != "Normal")
                {
                    hojaLecturas.Range(fila, 1, fila, 4).Style.Fill
                        .SetBackgroundColor(l.Estado == "Ruptura" ? RojoSuave : AmbarSuave);
                }
            }
            Terminar(hojaLecturas, lecturas.Count, 4, new[] { 26.0, 24, 18, 16 });
        }

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
