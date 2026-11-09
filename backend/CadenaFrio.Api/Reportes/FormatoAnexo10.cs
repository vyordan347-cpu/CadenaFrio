using System.Globalization;
using ClosedXML.Excel;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Servicios;
using static CadenaFrio.Api.Reportes.EstiloExcel;

namespace CadenaFrio.Api.Reportes;

public record DatosAnexo10(
    string Equipo,
    string Ubicacion,
    TipoEquipo TipoEquipo,
    int Anio,
    int Mes,
    IReadOnlyList<HojaControlTemperatura.Dia> Dias,
    IReadOnlyList<HistoriaRangos.Tramo> Rangos,
    OpcionesEstablecimiento Establecimiento);

// "Hoja de control y registro diario de la temperatura de refrigeración" (Anexo N° 10 de la
// NTS N° 242-MINSA/DGIESP-2026), llenada con las lecturas del sensor: un día por fila y dos
// turnos (mañana y tarde). Las celdas amarillas son datos que el sistema no conoce y se
// completan a mano (marca, modelo, código patrimonial, responsable).
public static class FormatoAnexo10
{
    private static readonly XLColor AmarilloAMano = XLColor.FromHtml("#FFF7D6");
    private static readonly XLColor GrisEtiqueta = XLColor.FromHtml("#52606B");
    private static readonly XLColor Borde = XLColor.FromHtml("#C3CDD5");
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");

    public static byte[] Generar(DatosAnexo10 d)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Anexo 10");

        hoja.Cell(1, 1).Value = "ANEXO N° 10 · HOJA DE CONTROL Y REGISTRO DIARIO DE LA TEMPERATURA DE REFRIGERACIÓN";
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(AzulMarino);
        hoja.Cell(2, 1).Value = "NTS N° 242-MINSA/DGIESP-2026 · Llenada automáticamente con las lecturas del sensor";
        hoja.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);

        // ---- Datos generales (dos columnas de etiqueta/valor) ----
        var est = d.Establecimiento;
        var mes = new DateTime(d.Anio, d.Mes, 1).ToString("MMMM 'de' yyyy", Peru);
        var rango = d.Rangos.Count == 1
            ? $"Óptimo {d.Rangos[0].Limites.OptimoMinimo:0.0} a {d.Rangos[0].Limites.OptimoMaximo:0.0} °C; límite {d.Rangos[0].Limites.LimiteMinimo:0.0} a {d.Rangos[0].Limites.LimiteMaximo:0.0} °C"
            : "Cambió en el mes (ver nota al pie)";

        var datos = new (string Etiqueta, string Valor, bool AMano)[]
        {
            ("Establecimiento de salud", est.Nombre, string.IsNullOrWhiteSpace(est.Nombre)),
            ("DIRESA / GERESA / DIRIS", est.DiresaGeresaDiris, string.IsNullOrWhiteSpace(est.DiresaGeresaDiris)),
            ("Red de salud", est.RedSalud, string.IsNullOrWhiteSpace(est.RedSalud)),
            ("Microrred", est.Microrred, string.IsNullOrWhiteSpace(est.Microrred)),
            ("Mes de control", char.ToUpper(mes[0]) + mes[1..], false),
            ("Equipo", d.TipoEquipo == TipoEquipo.Termo ? $"{d.Equipo} (termo)" : d.Equipo, false),
            ("Ubicación", d.Ubicacion, string.IsNullOrWhiteSpace(d.Ubicacion)),
            ("Rango de temperatura", rango, false),
            ("Tipo (eléctrico / solar)", "", true),
            ("Marca y modelo", "", true),
            ("Código patrimonial", "", true),
            ("Responsable del control", "", true)
        };

        var fila = 4;
        foreach (var (etiqueta, valor, aMano) in datos)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.SetBold().Font.SetFontColor(GrisEtiqueta);
            var celda = hoja.Range(fila, 2, fila, 7).Merge();
            celda.Value = valor;
            if (aMano)
            {
                celda.Style.Fill.SetBackgroundColor(AmarilloAMano);
            }
            celda.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Borde);
            fila++;
        }

        // ---- Registro diario: un día por fila, mañana (8–14 h) y tarde (14–20 h) ----
        fila++;
        var inicioTabla = fila;
        var titulos = new[] { "Día", "Mañana: hora", "Mañana: °C", "Mañana: estado", "Tarde: hora", "Tarde: °C", "Tarde: estado" };
        for (var c = 0; c < titulos.Length; c++)
        {
            hoja.Cell(fila, c + 1).Value = titulos[c];
        }
        var cabecera = hoja.Range(fila, 1, fila, titulos.Length);
        cabecera.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(AzulMarino);
        cabecera.Style.Alignment.SetWrapText(true);
        fila++;

        foreach (var dia in d.Dias)
        {
            hoja.Cell(fila, 1).Value = dia.Fecha.ToDateTime(TimeOnly.MinValue);
            hoja.Cell(fila, 1).Style.NumberFormat.Format = "ddd dd/mm";
            EscribirTurno(hoja, fila, 2, dia.Manana);
            EscribirTurno(hoja, fila, 5, dia.Tarde);
            fila++;
        }

        var tabla = hoja.Range(inicioTabla, 1, fila - 1, titulos.Length);
        tabla.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(Borde);
        tabla.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Borde);
        hoja.SheetView.FreezeRows(inicioTabla);

        // ---- Notas ----
        fila++;
        var notas = new List<string>
        {
            "Turnos de control: mañana de 8:00 a 14:00 y tarde de 14:00 a 20:00. En cada turno se anota la primera lectura del sensor.",
            "\"Salió del límite en el turno\": en algún momento del turno hubo una lectura fuera del límite, aunque la anotada esté dentro.",
            "\"Sin datos\": en ese turno no llegó ninguna lectura del sensor; no se sabe qué temperatura hubo."
        };
        if (d.Rangos.Count > 1)
        {
            notas.Add("El rango cambió en el mes; cada lectura se juzga con el que regía en ese momento: " + string.Join("; ",
                d.Rangos.Select(t => $"desde {Local(t.Desde):dd/MM HH:mm} óptimo {t.Limites.OptimoMinimo:0.0}–{t.Limites.OptimoMaximo:0.0} °C, límite {t.Limites.LimiteMinimo:0.0}–{t.Limites.LimiteMaximo:0.0} °C")) + ".");
        }
        notas.Add($"Generado por el sistema CadenaFrio el {Local(DateTime.UtcNow):dd/MM/yyyy HH:mm}. Las celdas amarillas se completan a mano.");

        foreach (var nota in notas)
        {
            var celda = hoja.Range(fila, 1, fila, titulos.Length).Merge();
            celda.Value = nota;
            celda.Style.Alignment.SetWrapText(true).Font.SetFontColor(GrisEtiqueta);
            hoja.Row(fila).Height = 30;
            fila++;
        }

        hoja.Column(1).Width = 26;
        foreach (var c in new[] { 2, 5 }) hoja.Column(c).Width = 12;
        foreach (var c in new[] { 3, 6 }) hoja.Column(c).Width = 11;
        foreach (var c in new[] { 4, 7 }) hoja.Column(c).Width = 30;
        hoja.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    // Hora, temperatura y estado de un turno, con su color: rojo si salió del límite,
    // ámbar si la lectura anotada estuvo fuera del rango recomendado, gris si no hubo datos.
    private static void EscribirTurno(IXLWorksheet hoja, int fila, int columna, HojaControlTemperatura.Turno t)
    {
        if (!t.HayDatos)
        {
            hoja.Cell(fila, columna + 2).Value = "Sin datos";
            hoja.Range(fila, columna, fila, columna + 2).Style.Fill.SetBackgroundColor(GrisSuave);
            hoja.Cell(fila, columna + 2).Style.Font.SetItalic();
            return;
        }

        hoja.Cell(fila, columna).Value = Local(t.HoraLectura!.Value);
        hoja.Cell(fila, columna).Style.NumberFormat.Format = "hh:mm";
        hoja.Cell(fila, columna + 1).Value = t.Temperatura!.Value;
        hoja.Cell(fila, columna + 1).Style.NumberFormat.Format = "0.0";

        var estado = t.Nivel switch
        {
            NivelAlerta.Ruptura => "Fuera del límite",
            NivelAlerta.Advertencia => "Fuera del rango recomendado",
            _ => "Normal"
        };
        if (t.SalioDelLimite && t.Nivel != NivelAlerta.Ruptura)
        {
            estado += " · salió del límite en el turno";
        }
        hoja.Cell(fila, columna + 2).Value = estado;

        var color = t.SalioDelLimite ? RojoSuave : t.Nivel == NivelAlerta.Advertencia ? AmbarSuave : null;
        if (color is not null)
        {
            hoja.Range(fila, columna, fila, columna + 2).Style.Fill.SetBackgroundColor(color);
        }
    }
}
