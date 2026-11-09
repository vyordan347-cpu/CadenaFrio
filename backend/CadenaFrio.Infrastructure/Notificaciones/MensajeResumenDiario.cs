using System.Globalization;
using System.Text;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Cifras de un equipo en el día del resumen. Lecturas esperadas: una por minuto.
public record EquipoDelResumen(
    string Nombre,
    int Lecturas,
    int Esperadas,
    double? Minima,
    double? Maxima,
    double? PorcentajeOptimo,
    int Rupturas);

// Una alerta que sigue esperando revisión. Plazo: solo las rupturas del Anexo 13 (24 h desde la detección).
public record PendienteDelResumen(string Equipo, string Que, DateTime? PlazoUtc);

// Texto del resumen diario por Telegram (HTML simple: <b>). Solo hechos: nunca dice que las
// vacunas estén seguras, y un equipo sin lecturas lo dice siempre.
public static class MensajeResumenDiario
{
    public static string Armar(DateOnly dia, string establecimiento, IReadOnlyList<EquipoDelResumen> equipos,
        IReadOnlyList<PendienteDelResumen> pendientes, DateTime ahoraUtc)
    {
        var texto = new StringBuilder();
        texto.Append($"📋 <b>Resumen del {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}</b>");
        // El día en curso no terminó: se aclara hasta qué hora van las cifras.
        if (dia == DateOnly.FromDateTime(ahoraUtc.AddHours(-5)))
        {
            texto.Append($" (hasta las {ahoraUtc.AddHours(-5).ToString("HH:mm", CultureInfo.InvariantCulture)})");
        }
        if (!string.IsNullOrWhiteSpace(establecimiento))
        {
            texto.Append($" — {Html(establecimiento)}");
        }
        texto.Append('\n');

        if (equipos.Count == 0)
        {
            texto.Append("\nNo hay equipos activos.\n");
        }

        foreach (var e in equipos)
        {
            texto.Append($"\n<b>{Html(e.Nombre)}</b>\n");
            if (e.Lecturas == 0)
            {
                texto.Append("• Sin lecturas en todo el día\n");
            }
            else
            {
                texto.Append($"• Mín {Grados(e.Minima)} · Máx {Grados(e.Maxima)}\n");
                if (e.PorcentajeOptimo is { } p)
                {
                    texto.Append($"• {p.ToString("0.#", CultureInfo.InvariantCulture)} % de las lecturas en rango óptimo\n");
                }
                texto.Append($"• Lecturas: {Numero(e.Lecturas)} de {Numero(e.Esperadas)} esperadas\n");
            }

            if (e.Rupturas > 0)
            {
                texto.Append(e.Rupturas == 1 ? "• 1 ruptura de temperatura\n" : $"• {e.Rupturas} rupturas de temperatura\n");
            }
        }

        if (pendientes.Count == 0)
        {
            texto.Append("\n✅ Sin revisiones pendientes.");
        }
        else
        {
            texto.Append($"\n⚠️ <b>Revisiones pendientes: {pendientes.Count}</b>\n");
            foreach (var p in pendientes)
            {
                texto.Append($"• {Html(p.Equipo)}, {Html(p.Que)}");
                if (p.PlazoUtc is { } plazo)
                {
                    texto.Append(plazo > ahoraUtc
                        ? $": notificar antes del {MensajesAlerta.Hora(plazo)}"
                        : $": plazo vencido el {MensajesAlerta.Hora(plazo)}");
                }
                texto.Append('\n');
            }
            texto.Append("Revíselas en el sistema, en Alertas.");
        }

        return texto.ToString().TrimEnd();
    }

    // Telegram solo exige escapar <, > y &; así la "ñ" y las tildes quedan legibles.
    private static string Html(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Grados(double? valor) =>
        valor is { } v ? $"{v.ToString("0.#", CultureInfo.InvariantCulture)} °C" : "—";

    // 1440 → "1 440": separador de miles con espacio, como en Perú.
    private static string Numero(int valor) => valor.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');
}
