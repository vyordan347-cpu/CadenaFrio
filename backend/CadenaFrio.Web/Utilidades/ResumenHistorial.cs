using System.Globalization;
using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Web.Utilidades;

// Una frase del resumen del historial: lo que pasó, dicho en palabras simples.
// Clase: "rup", "sen", "adv" u "ok" (define el ícono y el borde, no solo el color).
public record FraseResumen(string Clase, string Icono, string Titulo, string Detalle);

// Arma el resumen del periodo con los datos que ya trae el historial. Reglas:
// - Solo hechos medidos o registrados; ninguna conclusión clínica.
// - Nunca dice que las vacunas están seguras: una lectura dentro del rango describe solo ese momento.
// - Lo que requiere acción va primero; los periodos sin datos se dicen siempre.
public static class ResumenHistorial
{
    public static List<FraseResumen> Frases(HistorialDto h, IReadOnlyList<(DateTime Desde, DateTime Hasta)> huecos)
    {
        var e = h.Estadisticas;
        var frases = new List<FraseResumen>();

        // ¿Cambió el rango en el periodo? Entonces cada lectura se juzgó con el de su momento
        // (lo hace la API) y no tiene sentido comparar la máxima con un solo límite.
        var rangoCambio = h.Rangos.Count > 1;
        var fueraDelLimite = e.CantidadFueraDelLimite;

        // 1. Rupturas (o lecturas fuera del límite sin alerta en el periodo).
        if (e.CantidadRupturas > 0 || fueraDelLimite > 0)
        {
            var partes = new List<string>();
            if (fueraDelLimite > 0)
            {
                partes.Add(fueraDelLimite == 1
                    ? "1 lectura quedó fuera del límite permitido."
                    : $"{fueraDelLimite.ToString("N0", CultureInfo.InvariantCulture)} lecturas quedaron fuera del límite permitido.");
            }
            if (!rangoCambio && e.Maxima is { } max && max > h.LimiteMaximo)
            {
                partes.Add($"La temperatura subió hasta {G(max)} (el límite es {G(h.LimiteMaximo)}).");
            }
            if (!rangoCambio && e.Minima is { } min && min < h.LimiteMinimo)
            {
                partes.Add($"La temperatura bajó hasta {G(min)} (el límite es {G(h.LimiteMinimo)}).");
            }
            if (rangoCambio && fueraDelLimite > 0 && e.Minima is { } mn && e.Maxima is { } mx)
            {
                partes.Add($"La temperatura más baja fue {G(mn)} y la más alta {G(mx)}.");
            }
            // El tiempo sale de cuánto estuvieron abiertas las alertas, no de lecturas continuas:
            // si hubo huecos, se aclara que no se sabe qué pasó en parte de ese tiempo.
            if (e.MinutosEnRuptura > 0)
            {
                var duracion = FormatoFecha.Duracion(TimeSpan.FromMinutes(e.MinutosEnRuptura));
                partes.Add(huecos.Count > 0
                    ? $"Las alertas de ruptura estuvieron abiertas {duracion} en total; en parte de ese tiempo no hubo lecturas."
                    : $"Las alertas de ruptura estuvieron abiertas {duracion} en total.");
            }

            var sinRevisar = h.Alertas.Count(a => a.Nivel == NivelAlertaDto.Ruptura && a.Tipo != TipoAlertaDto.SinSenal
                                                  && a.RequiereRevision && a.AtendidaPor is null);
            if (sinRevisar > 0)
            {
                partes.Add(sinRevisar == 1 ? "Esta ruptura todavía no se ha revisado." : $"{sinRevisar} rupturas todavía no se han revisado.");
            }

            var titulo = e.CantidadRupturas switch
            {
                0 => "Hubo lecturas fuera del límite permitido.",
                1 => "Hubo 1 ruptura de temperatura.",
                var n => $"Hubo {n} rupturas de temperatura."
            };
            frases.Add(new("rup", "ruptura", titulo, string.Join(" ", partes)));
        }

        // 2. Periodos sin datos: siempre se dicen, porque no se sabe qué pasó en ese tiempo.
        if (huecos.Count > 0)
        {
            var masLargo = huecos.Max(x => x.Hasta - x.Desde);
            var titulo = huecos.Count == 1 ? "Faltan datos en 1 periodo." : $"Faltan datos en {huecos.Count} periodos.";
            var detalle = huecos.Count == 1
                ? $"Fueron {FormatoFecha.Duracion(masLargo)} sin lecturas: no se sabe qué temperatura hubo en ese tiempo."
                : $"El más largo fue de {FormatoFecha.Duracion(masLargo)}. En esos periodos no se sabe qué temperatura hubo.";
            frases.Add(new("sen", "sin-senal", titulo, detalle));
        }

        // Cambio de rango: se avisa para que nadie compare las lecturas viejas con el rango de hoy.
        if (rangoCambio)
        {
            var cambios = h.Rangos.Count - 1;
            frases.Add(new("info", "info",
                cambios == 1 ? "El rango del equipo cambió en este periodo." : $"El rango del equipo cambió {cambios} veces en este periodo.",
                "Cada lectura se juzga con el rango que regía cuando se tomó. Los cambios se ven en el gráfico y en el Excel."));
        }

        // 3. Fuera del rango recomendado, pero sin pasar el límite.
        var fueraDelOptimo = e.PorcentajeOptimo is { } po && po < 100;
        if (fueraDelOptimo && frases.All(f => f.Clase != "rup"))
        {
            frases.Add(new("adv", "advertencia", "Algunas lecturas salieron del rango recomendado.",
                rangoCambio
                    ? "Estuvieron fuera del rango recomendado de su momento, pero sin pasar el límite permitido."
                    : $"Estuvieron fuera de {G(h.OptimoMinimo)} a {G(h.OptimoMaximo)}, pero sin pasar el límite permitido."));
        }

        // 4. Sin problemas registrados: se dice qué significa y qué no.
        if (frases.All(f => f.Clase != "rup") && e.CantidadLecturas > 0)
        {
            var donde = rangoCambio
                ? "Dentro del límite que regía en cada momento."
                : $"Entre {G(h.LimiteMinimo)} y {G(h.LimiteMaximo)}.";
            frases.Add(new("ok", "ok", "Todas las lecturas registradas estuvieron dentro del límite permitido.",
                $"{donde} Esto describe solo los momentos con datos; por sí solo no confirma el estado de las vacunas."));
        }

        return frases;
    }

    // Lecturas que se esperarían en el periodo, a una por minuto (hasta ahora si el periodo
    // termina en el futuro). Es un conteo de lecturas, no un porcentaje de tiempo.
    public static int LecturasEsperadas(HistorialDto h)
    {
        var fin = h.Hasta < DateTime.UtcNow ? h.Hasta : DateTime.UtcNow;
        return Math.Max((int)Math.Floor((fin - h.Desde).TotalMinutes), 0);
    }

    private static string G(double valor) => valor.ToString("0.#", CultureInfo.InvariantCulture) + " °C";
}
