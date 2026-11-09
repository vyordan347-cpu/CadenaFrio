using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Web.Utilidades;

// Cuentas sobre los tramos que envía la API del historial. Las usan el gráfico y el resumen
// de la página, así los dos dicen lo mismo sobre los periodos sin datos.
public static class TramosHistorial
{
    // La API agrupa las lecturas en unos 300 tramos iguales del periodo (mínimo 1 minuto) y
    // da la hora de INICIO de cada tramo. Es la misma cuenta que HistorialController.Resumir;
    // si allí cambia, hay que cambiarla aquí.
    private const int PuntosObjetivo = 300;

    // Más de 5 minutos sin datos es "sin señal" (valor por defecto de la configuración).
    private static readonly TimeSpan SinSenal = TimeSpan.FromMinutes(5);

    public static TimeSpan Tramo(DateTime desde, DateTime hasta) =>
        TimeSpan.FromTicks(Math.Max(TimeSpan.FromMinutes(1).Ticks, (hasta - desde).Ticks / PuntosObjetivo));

    // ¿Hay un hueco sin datos entre dos tramos? Sí, si en medio quedó al menos un tramo vacío
    // y además el hueco supera los 5 minutos (con tramos de 1 minuto, una lectura que llega
    // unos segundos tarde no es un hueco).
    public static bool HayHueco(DateTime inicioAnterior, DateTime inicioSiguiente, TimeSpan tramo)
    {
        var distancia = inicioSiguiente - inicioAnterior;
        return distancia >= tramo * 2 && distancia > tramo + SinSenal;
    }

    // Periodos sin datos: antes de la primera lectura, entre tramos y después de la última
    // (hasta el fin del periodo, o hasta ahora si el periodo termina en el futuro).
    // Sin ninguna lectura no hay huecos que mostrar: la página muestra su estado vacío.
    public static List<(DateTime Desde, DateTime Hasta)> Huecos(IReadOnlyList<PuntoHistorialDto> puntos, DateTime desde, DateTime hasta)
    {
        var huecos = new List<(DateTime, DateTime)>();
        if (puntos.Count == 0)
        {
            return huecos;
        }

        var tramo = Tramo(desde, hasta);
        var fin = hasta < DateTime.UtcNow ? hasta : DateTime.UtcNow;
        if (HayHueco(desde - tramo, puntos[0].Fecha, tramo))
        {
            huecos.Add((desde, puntos[0].Fecha));
        }
        for (var i = 1; i < puntos.Count; i++)
        {
            if (HayHueco(puntos[i - 1].Fecha, puntos[i].Fecha, tramo))
            {
                huecos.Add((puntos[i - 1].Fecha + tramo, puntos[i].Fecha));
            }
        }
        if (HayHueco(puntos[^1].Fecha, fin, tramo))
        {
            huecos.Add((puntos[^1].Fecha + tramo, fin));
        }
        return huecos;
    }
}
