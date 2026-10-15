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
}
