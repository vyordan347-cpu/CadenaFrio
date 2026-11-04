using ClosedXML.Excel;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Configuracion;

namespace CadenaFrio.Api.Reportes;

// Lo que el sistema sabe de una ruptura, reunido por el controlador.
// Las temperaturas son las de las lecturas en esos momentos; nulas si no hay lectura.
public record DatosAnexo13(
    Alerta Alerta,
    IReadOnlyList<string> Vacunas,
    double? TemperaturaDeteccion,
    DateTime? FechaUltimoRegistroNormal,
    double? TemperaturaUltimoRegistroNormal,
    double? TemperaturaRestablecimiento,
    string? CorreoNotificador,
    OpcionesEstablecimiento Establecimiento);

// "Formato de notificación de ruptura de cadena de frío" (Anexo N° 13 de la NTS N° 242-MINSA/DGIESP-2026),
// prellenado con lo que registra el sistema. Sigue el orden del formato oficial; las celdas
// amarillas son datos que el sistema no conoce y se completan a mano (marca, lotes, stock, cargo).
public static class FormatoAnexo13
{
    private const string Marcada = "☒";
    private const string Vacia = "☐";
    private const int Columnas = 4;

    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);
    private static readonly XLColor AzulMarino = XLColor.FromHtml("#14304A");
    private static readonly XLColor GrisEtiqueta = XLColor.FromHtml("#52606B");
    private static readonly XLColor AmarilloAMano = XLColor.FromHtml("#FFF7D6");
    private static readonly XLColor Borde = XLColor.FromHtml("#C3CDD5");

    public static byte[] Generar(DatosAnexo13 datos)
    {
        var a = datos.Alerta;
        var est = datos.Establecimiento;
        var esTermo = a.Equipo.Tipo == TipoEquipo.Termo;

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Anexo 13");
        var h = new Escritor(hoja);

        hoja.Cell(1, 1).Value = "ANEXO N° 13 · FORMATO DE NOTIFICACIÓN DE RUPTURA DE CADENA DE FRÍO";
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(AzulMarino);
        hoja.Cell(2, 1).Value = "NTS N° 242-MINSA/DGIESP-2026 · Norma técnica de salud para el manejo de la cadena de frío en inmunizaciones";
        hoja.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);
        h.Fila = 4;

        // ---- Establecimiento ----
        h.Seccion("Datos del establecimiento");
        h.Campo("Nombre del establecimiento", Texto(est.Nombre));
        h.Campo("Fecha de notificación", Local(DateTime.UtcNow).Date, "dd/mm/yyyy");
        h.Opciones("Tipo de establecimiento",
            OpcionesEstablecimiento.TiposEstablecimiento.Select(t => (t, string.Equals(t, est.Tipo, StringComparison.OrdinalIgnoreCase))));
        h.Campo("DIRESA / GERESA / DIRIS", Texto(est.DiresaGeresaDiris));
        h.Campo("Red de salud", Texto(est.RedSalud));
        h.Campo("Microrred de salud", Texto(est.Microrred));
        h.Campo("Departamento", Texto(est.Departamento));
        h.Campo("Provincia", Texto(est.Provincia));
        h.Campo("Distrito", Texto(est.Distrito));
        h.Campo("Teléfono fijo", Texto(est.TelefonoFijo));
        h.Campo("Correo electrónico", Texto(est.Correo));
        h.Campo("Celular", Texto(est.Celular));

        // ---- Descripción de la ruptura ----
        // El dispositivo del sistema cumple el papel de data logger: registra la temperatura cada minuto.
        h.Seccion("Descripción de la ruptura de la cadena de frío");
        h.Opciones("Momento", new[]
        {
            ("Ruptura durante la conservación de vacunas", !esTermo),
            ("Ruptura durante el transporte de vacunas", false)
        });
        h.Opciones("Detección", new[]
        {
            ("Ruptura detectada por el EESS", false),
            ("Ruptura detectada por lectura de data logger", true)
        });
        h.Campo("Tiene data logger en el refrigerador y/o caja transportadora", "Sí (dispositivo de monitoreo del sistema)");

        h.Tabla(new[] { "", "Fecha", "Hora", "Temperatura (°C)" });
        h.FilaMomento("Fecha en que detectó la ruptura de la cadena de frío", a.FechaRuptura, datos.TemperaturaDeteccion ?? a.ValorExtremo);
        h.FilaMomento("Fecha del último registro en la hoja de temperatura (último registro normal)",
            datos.FechaUltimoRegistroNormal, datos.TemperaturaUltimoRegistroNormal);
        h.FilaMomento("Fecha en que se restablece la cadena de frío", a.Fin, datos.TemperaturaRestablecimiento);

        h.Campo("Temperatura más alejada del rango durante el episodio (°C)", a.ValorExtremo, "0.0");
        h.Campo("Las vacunas están inmovilizadas",
            a.VacunasInmovilizadas switch { true => "Sí", false => "No", null => Blank.Value });
        h.Opciones("¿Cómo restableció la cadena de frío?",
            CatalogoAnexo13.FormasRestablecimiento.Select(o =>
                (ConDetalle(o, a.FormaRestablecimiento, a.DetalleRestablecimiento), o.Codigo == a.FormaRestablecimiento)));

        // ---- Tipo y causa de la ruptura ----
        h.Seccion("Tipo de ruptura de cadena de frío");
        h.Opciones("Tipo", new[]
        {
            ("Ruptura en el equipo de refrigeración", !esTermo),
            ("Ruptura en complemento de cadena de frío", esTermo)
        });

        if (esTermo)
        {
            h.Opciones("Complemento", new[] { ("Caja transportadora", false), ("Termo porta vacunas", true) });
            h.Campo("Termómetro de alcohol (Sí / No)", Blank.Value, aMano: true);
            h.Campo("Termómetro digital (Sí / No)", Blank.Value, aMano: true);
            h.Campo("Data logger (Sí / No)", "Sí");
            h.Campo("Hoja de control de registro de temperatura (Sí / No)", Blank.Value, aMano: true);
            h.Campo("PF congelados / parcialmente congelados / totalmente descongelados", Blank.Value, aMano: true);
            h.Campo("Nro. de PF de agua utilizados", Blank.Value, aMano: true);
            h.Campo("Nro. de PF eutécticos utilizados", Blank.Value, aMano: true);
            h.Opciones("Ruptura durante", new[] { ("Almacenaje en EE.SS.", false), ("Brigada", false), ("Transporte", false) });
        }
        else
        {
            h.Opciones("Equipo", new[]
            {
                ("Refrigerador", true), ("Congelador", false),
                ("Refrigerador fotovoltaico (solar)", false), ("Cámara frigorífica para vacunas", false)
            });
        }

        h.Campo("Equipo en el sistema", $"{a.Equipo.Nombre} ({a.Equipo.Ubicacion})");
        h.Campo("Marca", Blank.Value, aMano: true);
        h.Campo("Modelo", Blank.Value, aMano: true);

        h.Opciones("Causa de la ruptura",
            CatalogoAnexo13.CausasPara(a.Equipo.Tipo).Select(o =>
                (ConDetalle(o, a.CausaRuptura, a.DetalleCausa), o.Codigo == a.CausaRuptura)));

        // ---- Vacunas ----
        h.Seccion("Inventario de vacunas por equipo de cadena de frío");
        h.Tabla(new[] { "Vacuna", "Presentación (Fco. N° de dosis)", "Lote", "Caduca (vencimiento)", "Stock (N° de frascos)" }, columnas: 5);
        var vacunas = datos.Vacunas.Count > 0 ? datos.Vacunas : new[] { "", "", "" };
        foreach (var vacuna in vacunas)
        {
            h.FilaVacuna(vacuna);
        }

        // ---- Comentarios y responsable ----
        h.Seccion("Comentarios");
        var comentarios = new List<string>();
        if (!string.IsNullOrWhiteSpace(a.ObservacionAtencion)) comentarios.Add(a.ObservacionAtencion);
        if (a.DetectadaTarde) comentarios.Add("La ruptura se conoció al sincronizar lecturas guardadas sin conexión.");
        comentarios.Add($"Rango límite vigente: {a.LimiteMinimo:0.0} a {a.LimiteMaximo:0.0} °C. Rango óptimo: {a.OptimoMinimo:0.0} a {a.OptimoMaximo:0.0} °C.");
        h.Parrafo(string.Join(Environment.NewLine, comentarios));

        h.Seccion("Responsable de la notificación");
        h.Tabla(new[] { "Nombre del notificador", "Cargo", "Teléfono", "Correo electrónico" });
        h.FilaResponsable(a.AtendidaPor ?? "", datos.CorreoNotificador ?? "");

        // ---- Pie ----
        h.Fila++;
        var limite = a.RevisionRequeridaDesde + CatalogoAnexo13.PlazoNotificacion;
        var pie = $"Generado por el sistema CadenaFrio el {Local(DateTime.UtcNow):dd/MM/yyyy HH:mm}. "
                  + (limite is { } l ? $"Plazo de notificación: hasta el {Local(l):dd/MM/yyyy HH:mm} (24 h, NTS 242 numeral 6.10.2.1). " : "")
                  + "Las celdas amarillas se completan a mano.";
        h.Nota(pie);

        hoja.Column(1).Width = 46;
        for (var c = 2; c <= 5; c++) hoja.Column(c).Width = 20;
        hoja.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    // "Otra causa: se dejó el termo al sol" cuando la opción elegida lleva detalle.
    private static string ConDetalle(OpcionAnexo13 opcion, string? elegida, string? detalle)
    {
        var texto = opcion.Grupo is null ? opcion.Descripcion : $"{opcion.Grupo}: {opcion.Descripcion}";
        return opcion.Codigo == elegida && !string.IsNullOrWhiteSpace(detalle) ? $"{texto}: {detalle}" : texto;
    }

    // Un dato vacío de la configuración se deja en blanco para llenarlo a mano.
    private static XLCellValue Texto(string valor) => string.IsNullOrWhiteSpace(valor) ? Blank.Value : valor;

    // Excel no maneja zonas horarias: se escribe directamente la hora de Perú.
    private static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc + DesfasePeru, DateTimeKind.Unspecified);

    // Escribe la hoja de arriba hacia abajo, una fila tras otra.
    private sealed class Escritor(IXLWorksheet hoja)
    {
        public int Fila { get; set; } = 1;

        public void Seccion(string titulo)
        {
            Fila++;
            var rango = hoja.Range(Fila, 1, Fila, Columnas + 1).Merge();
            rango.Value = titulo.ToUpperInvariant();
            rango.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
            rango.Style.Fill.SetBackgroundColor(AzulMarino);
            Fila++;
        }

        // Etiqueta a la izquierda, valor en el resto de la fila.
        public void Campo(string etiqueta, XLCellValue valor, string? formato = null, bool aMano = false)
        {
            Etiqueta(etiqueta);
            var celda = hoja.Range(Fila, 2, Fila, Columnas + 1).Merge();
            celda.FirstCell().Value = valor;
            celda.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left).Alignment.SetWrapText(true);
            celda.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Borde);
            if (formato is not null) celda.Style.NumberFormat.Format = formato;
            if (aMano || valor.IsBlank) celda.Style.Fill.SetBackgroundColor(AmarilloAMano);
            Fila++;
        }

        // Lista de casillas del formato, una por fila, con la elegida marcada.
        public void Opciones(string etiqueta, IEnumerable<(string Texto, bool Elegida)> opciones)
        {
            Etiqueta(etiqueta);
            var primera = true;
            foreach (var (texto, elegida) in opciones)
            {
                if (!primera) Fila++;
                primera = false;
                var celda = hoja.Range(Fila, 2, Fila, Columnas + 1).Merge();
                celda.FirstCell().Value = $"{(elegida ? Marcada : Vacia)} {texto}";
                if (elegida) celda.Style.Font.SetBold();
            }
            Fila++;
        }

        public void Tabla(string[] titulos, int columnas = Columnas)
        {
            for (var i = 0; i < titulos.Length; i++)
            {
                hoja.Cell(Fila, i + 1).Value = titulos[i];
            }
            var rango = hoja.Range(Fila, 1, Fila, columnas);
            rango.Style.Font.SetBold().Font.SetFontColor(GrisEtiqueta).Alignment.SetWrapText(true);
            rango.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(Borde);
            Fila++;
        }

        // Fecha, hora y temperatura de un momento del episodio. Lo que no se conoce queda en amarillo.
        public void FilaMomento(string concepto, DateTime? fechaUtc, double? temperatura)
        {
            hoja.Cell(Fila, 1).Value = concepto;
            hoja.Cell(Fila, 1).Style.Alignment.SetWrapText(true).Font.SetFontColor(GrisEtiqueta);
            if (fechaUtc is { } f)
            {
                hoja.Cell(Fila, 2).Value = Local(f).Date;
                hoja.Cell(Fila, 2).Style.NumberFormat.Format = "dd/mm/yyyy";
                hoja.Cell(Fila, 3).Value = Local(f).TimeOfDay;
                hoja.Cell(Fila, 3).Style.NumberFormat.Format = "hh:mm";
            }
            else
            {
                hoja.Range(Fila, 2, Fila, 3).Style.Fill.SetBackgroundColor(AmarilloAMano);
            }

            if (temperatura is { } t)
            {
                hoja.Cell(Fila, 4).Value = t;
                hoja.Cell(Fila, 4).Style.NumberFormat.Format = "0.0";
            }
            else
            {
                hoja.Cell(Fila, 4).Style.Fill.SetBackgroundColor(AmarilloAMano);
            }
            hoja.Range(Fila, 2, Fila, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            Fila++;
        }

        public void FilaVacuna(string vacuna)
        {
            hoja.Cell(Fila, 1).Value = vacuna;
            hoja.Range(Fila, 2, Fila, 5).Style.Fill.SetBackgroundColor(AmarilloAMano);
            hoja.Range(Fila, 1, Fila, 5).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorderColor(Borde).Border.SetOutsideBorderColor(Borde);
            if (vacuna.Length == 0) hoja.Cell(Fila, 1).Style.Fill.SetBackgroundColor(AmarilloAMano);
            Fila++;
        }

        public void FilaResponsable(string nombre, string correo)
        {
            hoja.Cell(Fila, 1).Value = nombre;
            hoja.Cell(Fila, 4).Value = correo;
            hoja.Range(Fila, 2, Fila, 3).Style.Fill.SetBackgroundColor(AmarilloAMano);
            if (nombre.Length == 0) hoja.Cell(Fila, 1).Style.Fill.SetBackgroundColor(AmarilloAMano);
            if (correo.Length == 0) hoja.Cell(Fila, 4).Style.Fill.SetBackgroundColor(AmarilloAMano);
            Fila++;
        }

        public void Parrafo(string texto)
        {
            var rango = hoja.Range(Fila, 1, Fila, Columnas + 1).Merge();
            rango.Value = texto;
            rango.Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            hoja.Row(Fila).Height = 15 * (texto.Split(Environment.NewLine).Length + 1);
            Fila++;
        }

        public void Nota(string texto)
        {
            var rango = hoja.Range(Fila, 1, Fila, Columnas + 1).Merge();
            rango.Value = texto;
            rango.Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray).Alignment.SetWrapText(true);
            hoja.Row(Fila).Height = 30;
            Fila++;
        }

        private void Etiqueta(string texto)
        {
            hoja.Cell(Fila, 1).Value = texto;
            hoja.Cell(Fila, 1).Style.Font.SetBold().Font.SetFontColor(GrisEtiqueta)
                .Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        }
    }
}
