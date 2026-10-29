using System.Globalization;
using System.Net;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Textos de los avisos, compartidos por Telegram y correo: el mismo mensaje en ambos canales.
public static class MensajesAlerta
{
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    public static string Hora(DateTime utc) =>
        (utc + DesfasePeru).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

    private static string G(double valor) => valor.ToString("0.0", CultureInfo.InvariantCulture);

    // Solo se avisa por fuera del sistema lo que exige actuar: rupturas, pérdida de señal
    // y sensor dañado. Las advertencias se ven en el panel; avisarlas al celular causaría
    // "fatiga de alertas" y el personal terminaría ignorando los mensajes.
    public static bool EsUrgente(Alerta a) =>
        a.Tipo is TipoAlerta.SinSenal or TipoAlerta.SensorSinVariacion || a.Nivel == NivelAlerta.Ruptura;

    // Título y cuerpo en HTML simple (Telegram admite <b> e <i>).
    public static (string Titulo, string Cuerpo) Apertura(Alerta a, string equipo)
    {
        var e = WebUtility.HtmlEncode(equipo);
        return a.Tipo switch
        {
            TipoAlerta.SinSenal => ("📡 SIN SEÑAL DEL DISPOSITIVO",
                $"Equipo: <b>{e}</b>\nSin datos desde: {Hora(a.Inicio)}\nÚltima temperatura conocida: {G(a.ValorExtremo)} °C\n\n" +
                "Posible corte de energía o falla de conexión. Verifique el equipo."),
            TipoAlerta.SensorSinVariacion => ("⚠️ REVISAR SENSOR",
                $"Equipo: <b>{e}</b>\nEl sensor marca {G(a.ValorExtremo)} °C sin variar desde {Hora(a.Inicio)}.\n\n" +
                "Puede estar dañado. Verifique la temperatura con un termómetro."),
            _ => ("🔴 RUPTURA DE CADENA DE FRÍO",
                $"Equipo: <b>{e}</b>\nTemperatura: <b>{G(a.ValorExtremo)} °C</b> (límite {G(a.LimiteMinimo)} a {G(a.LimiteMaximo)} °C)\n" +
                $"Desde: {Hora(a.FechaRuptura ?? a.Inicio)}\n\n" +
                "Active el plan de contingencia y suspenda el uso de las vacunas hasta su evaluación (NTS N° 242).")
        };
    }

    public static (string Titulo, string Cuerpo) Escalamiento(Alerta a, string equipo, int minutos)
    {
        var (titulo, cuerpo) = Apertura(a, equipo);
        return ($"⏰ SIN ATENDER: {titulo}",
            $"Nadie confirmó la atención de esta alerta en {minutos} minutos.\n\n{cuerpo}");
    }

    public static string Cierre(Alerta a, string equipo)
    {
        var duracion = (a.Fin ?? DateTime.UtcNow) - a.Inicio;
        var que = a.Tipo switch
        {
            TipoAlerta.SinSenal => "recuperó la señal",
            TipoAlerta.SensorSinVariacion => "el sensor volvió a variar",
            _ => "la temperatura volvió al rango"
        };
        return $"✅ <b>{WebUtility.HtmlEncode(equipo)}</b>: {que} tras {Math.Round(duracion.TotalMinutes)} min.";
    }

    // HTML de Telegram a texto plano para el correo.
    public static string TextoPlano(string html) =>
        WebUtility.HtmlDecode(html.Replace("<b>", "").Replace("</b>", "").Replace("<i>", "").Replace("</i>", ""));
}
