namespace CadenaFrio.Web.Utilidades;
 
// Formatos de fecha reutilizables en todas las pantallas.
public static class FormatoFecha
{
    // Perú usa UTC-5 todo el año (no tiene horario de verano).
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);
 
    // Convierte una fecha UTC a hora de Perú, por ejemplo "15/10/2026 00:31:22".
    public static string HoraPeru(DateTime fechaUtc) =>
        (fechaUtc.ToUniversalTime() + DesfasePeru).ToString("dd/MM/yyyy HH:mm:ss");
}
 