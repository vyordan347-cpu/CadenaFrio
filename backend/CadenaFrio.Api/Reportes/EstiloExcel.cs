using ClosedXML.Excel;
using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Api.Reportes;

// Formato común de los Excel del sistema (Historial y Reportes), para que se vean igual.
// Se usa con "using static", así cada archivo los llama como si fueran propios.
public static class EstiloExcel
{
    public const string FormatoFecha = "dd/mm/yyyy hh:mm:ss";
    public static readonly XLColor AzulMarino = XLColor.FromHtml("#14304A");
    public static readonly XLColor RojoSuave = XLColor.FromHtml("#F8E3E1");
    public static readonly XLColor AmbarSuave = XLColor.FromHtml("#FBF0DC");
    public static readonly XLColor GrisSuave = XLColor.FromHtml("#ECEFF2");

    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    // Excel no maneja zonas horarias: se escribe directamente la hora de Perú.
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc + DesfasePeru, DateTimeKind.Unspecified);

    public static void Encabezados(IXLWorksheet hoja, params string[] titulos)
    {
        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(1, i + 1).Value = titulos[i];
        }

        var fila = hoja.Range(1, 1, 1, titulos.Length);
        fila.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        fila.Style.Fill.SetBackgroundColor(AzulMarino);
        fila.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText(true);
        hoja.Row(1).Height = 32;
    }

    // Fila de encabezado fija, filtros en cada columna y anchos legibles.
    public static void Terminar(IXLWorksheet hoja, int cantidadFilas, int columnas, double[] anchos)
    {
        hoja.SheetView.FreezeRows(1);

        if (cantidadFilas == 0)
        {
            hoja.Cell(2, 1).Value = "Sin datos en este periodo.";
            hoja.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        }
        else
        {
            hoja.Range(1, 1, cantidadFilas + 1, columnas).SetAutoFilter();
        }

        for (var i = 0; i < anchos.Length; i++)
        {
            hoja.Column(i + 1).Width = anchos[i];
        }
    }

    public static string NombreTipo(TipoAlerta tipo) => tipo switch
    {
        TipoAlerta.TemperaturaAlta => "Temperatura alta",
        TipoAlerta.TemperaturaBaja => "Temperatura baja",
        TipoAlerta.SinSenal => "Sin señal",
        TipoAlerta.SensorSinVariacion => "Sensor sin variación",
        _ => tipo.ToString()
    };

    // Texto apto para un nombre de archivo: "Refrigeradora principal" → "refrigeradora-principal".
    public static string Archivo(string nombre) =>
        string.Concat(nombre.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-'));
}
