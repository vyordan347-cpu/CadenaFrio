using System.Globalization;
using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Shared.Utilidades;

// ¿Se puede guardar una vacuna junto con las ya elegidas en el mismo equipo?
// Misma regla que la API (LimitesTemperatura.Interseccion): sus rangos óptimos deben cruzarse,
// es decir, debe existir una temperatura que sirva para todas. No inventa rangos: usa los del catálogo.
// Con intervalos basta revisar de a pares: si cada par se cruza, todos juntos se cruzan.
public static class CompatibilidadVacunas
{
    // Las vacunas elegidas con las que la candidata no se cruza (vacía si es compatible).
    public static List<VacunaDto> Conflictos(VacunaDto candidata, IEnumerable<VacunaDto> elegidas) =>
        elegidas
            .Where(e => e.Id != candidata.Id && !SeCruzan(candidata, e))
            .OrderBy(e => e.Nombre)
            .ToList();

    // Explicación en palabras simples, o null si la candidata es compatible.
    public static string? Motivo(VacunaDto candidata, IEnumerable<VacunaDto> elegidas)
    {
        var conflictos = Conflictos(candidata, elegidas);
        if (conflictos.Count == 0)
        {
            return null;
        }

        var otras = string.Join(" ni con ", conflictos.Select(c => $"{c.Nombre} {Optimo(c)}"));
        return $"Su óptimo {Optimo(candidata)} no se cruza con {otras}: " +
               "no hay una temperatura que sirva para todas.";
    }

    // Dos óptimos se cruzan si hay un tramo común (no basta con tocarse en un punto).
    private static bool SeCruzan(VacunaDto a, VacunaDto b) =>
        Math.Max(a.OptimoMinimo, b.OptimoMinimo) < Math.Min(a.OptimoMaximo, b.OptimoMaximo);

    private static string Optimo(VacunaDto v) =>
        $"({G(v.OptimoMinimo)}–{G(v.OptimoMaximo)} °C)";

    private static string G(double valor) => valor.ToString("0.0", CultureInfo.InvariantCulture);
}
