namespace CadenaFrio.Web.Utilidades;

// Formatos de fecha reutilizables en todas las pantallas.
public static class FormatoFecha
{
    // Perú usa UTC-5 todo el año (no tiene horario de verano).
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    // Convierte una fecha UTC a hora de Perú, por ejemplo "15/10/2026 00:31:22".
    public static string HoraPeru(DateTime fechaUtc) =>
        (fechaUtc.ToUniversalTime() + DesfasePeru).ToString("dd/MM/yyyy HH:mm:ss");

    // Texto relativo, más fácil de leer de un vistazo, por ejemplo "hace 3 min".
    public static string Antiguedad(DateTime fechaUtc)
    {
        var transcurrido = DateTime.UtcNow - fechaUtc.ToUniversalTime();

        if (transcurrido.TotalMinutes < 1) return "hace unos segundos";
        if (transcurrido.TotalMinutes < 60) return $"hace {(int)transcurrido.TotalMinutes} min";
        if (transcurrido.TotalHours < 24) return $"hace {(int)transcurrido.TotalHours} h";
        return $"hace {(int)transcurrido.TotalDays} días";
    }

    // Duración legible: "28 min", "8 h 38 min" o "2 días 3 h".
    public static string Duracion(TimeSpan duracion)
    {
        if (duracion.TotalMinutes < 1) return "menos de 1 min";
        if (duracion.TotalHours < 1) return $"{(int)duracion.TotalMinutes} min";
        if (duracion.TotalDays < 1)
        {
            return duracion.Minutes == 0
                ? $"{(int)duracion.TotalHours} h"
                : $"{(int)duracion.TotalHours} h {duracion.Minutes} min";
        }

        var dias = (int)duracion.TotalDays;
        return $"{dias} {(dias == 1 ? "día" : "días")} {duracion.Hours} h";
    }
}
