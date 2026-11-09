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

        var sobreLimite = e.Maxima is { } max && max > h.LimiteMaximo;
        var bajoLimite = e.Minima is { } min && min < h.LimiteMinimo;

        // 1. Rupturas (o lecturas fuera del límite sin alerta en el periodo).
        if (e.CantidadRupturas > 0 || sobreLimite || bajoLimite)
        {
            var partes = new List<string>();
            if (sobreLimite)
            {
                partes.Add($"La temperatura subió hasta {G(e.Maxima!.Value)} (el límite es {G(h.LimiteMaximo)}).");
            }
            if (bajoLimite)
            {
                partes.Add($"La temperatura bajó hasta {G(e.Minima!.Value)} (el límite es {G(h.LimiteMinimo)}).");
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

        // 3. Fuera del rango recomendado, pero sin pasar el límite.
        var fueraDelOptimo = e.PorcentajeOptimo is { } po && po < 100;
        if (fueraDelOptimo && frases.All(f => f.Clase != "rup"))
        {
            frases.Add(new("adv", "advertencia", "Algunas lecturas salieron del rango recomendado.",
                $"Estuvieron fuera de {G(h.OptimoMinimo)} a {G(h.OptimoMaximo)}, pero sin pasar el límite permitido."));
        }

        // 4. Sin problemas registrados: se dice qué significa y qué no.
        if (frases.All(f => f.Clase != "rup") && e.CantidadLecturas > 0)
        {
            frases.Add(new("ok", "ok", "Todas las lecturas registradas estuvieron dentro del límite permitido.",
                $"Entre {G(h.LimiteMinimo)} y {G(h.LimiteMaximo)}. Esto describe solo los momentos con datos; por sí solo no confirma el estado de las vacunas."));
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
