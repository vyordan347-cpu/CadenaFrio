using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Web.Utilidades;

// Estado visible de un equipo: etiqueta, color, ícono y acción a tomar.
// Lo usan el panel de todos los equipos y el detalle de cada uno, así la regla es una sola.
public record VistaEstado(
    string Estado,
    string Clase,      // "ok", "adv", "rup" o "sen": define los colores
    string Icono,
    string Accion,
    string Momento,
    bool Atenuada,
    int Gravedad);     // Para ordenar: los equipos con problemas primero

// Una de las dos dimensiones del estado que el panel muestra por separado:
// térmica (¿la temperatura está bien?) y comunicación (¿llegan datos?).
public record Dimension(string Texto, string Clase, string Icono);

public static class EstadoEquipo
{
    // La última lectura no es reciente: la temperatura mostrada es la última conocida,
    // no una medición en tiempo real, y la pantalla debe dejarlo claro.
    public static bool LecturaAntigua(TemperaturaActualDto d) =>
        d.Estado != EstadoLectura.Actualizada || d.SinSenalDesde is not null;

    // "hace 18 días" si la lectura es antigua; nulo si es reciente o no hay lecturas.
    // Acompaña al estado térmico para que no se lea como una medición de ahora.
    public static string? Antiguedad(TemperaturaActualDto d) =>
        LecturaAntigua(d) && d.FechaMedicion is { } f ? FormatoFecha.Antiguedad(f) : null;

    // ¿La temperatura está dentro del rango? Según la alerta activa (la misma regla de siempre).
    public static Dimension Termico(TemperaturaActualDto d) => d switch
    {
        { AlertaActiva.Nivel: NivelAlertaDto.Ruptura } => new("Ruptura", "rup", "ruptura"),
        { AlertaActiva.Nivel: NivelAlertaDto.Advertencia } => new("Advertencia", "adv", "advertencia"),
        { SensorSinVariacionDesde: not null } => new("Revisar sensor", "sen", "advertencia"),
        { Temperatura: null } => new("Sin datos", "sen", "sin-senal"),
        _ => new("Normal", "ok", "ok")
    };

    // ¿Llegan datos del dispositivo?
    public static Dimension Comunicacion(TemperaturaActualDto d) => d switch
    {
        { Estado: EstadoLectura.SinDatos } => new("Sin lecturas", "sen", "sin-senal"),
        { SinSenalDesde: not null } => new("Sin señal", "sen", "sin-senal"),
        { Estado: EstadoLectura.Desactualizada } => new("Retrasada", "adv", "reloj"),
        _ => new("En línea", "ok", "ok")
    };

    // Por orden de gravedad: ruptura > sin señal > sensor sin variación > advertencia > sin datos recientes > normal.
    public static VistaEstado Calcular(TemperaturaActualDto d)
    {
        var momento = d.FechaMedicion is { } f
            ? $"Última lectura {FormatoFecha.Antiguedad(f)}"
            : "Sin lecturas todavía";

        if (d.AlertaActiva is { Nivel: NivelAlertaDto.Ruptura } rup)
        {
            var sentido = rup.Tipo == TipoAlertaDto.TemperaturaAlta ? "Sobre" : "Bajo";
            // Con la lectura antigua no se sabe si la ruptura sigue: se dice cuál fue el último
            // estado conocido y se pide verificar, en vez de hablar como si fuera de ahora.
            var accion = Antiguedad(d) is { } hace
                ? $"Último estado conocido: {sentido.ToLowerInvariant()} el límite. Sin lecturas desde {hace}: revise el equipo y verifique con un termómetro."
                : $"Active el plan de contingencia. {sentido} el límite desde {FormatoFecha.Antiguedad(rup.FechaRuptura ?? rup.Inicio)}.";
            return new("Ruptura", "rup", "ruptura", accion, momento, false, 5);
        }

        if (d.SinSenalDesde is { } desde)
        {
            return new("Sin señal", "sen", "sin-senal",
                $"Posible corte de energía o falla de conexión. Sin datos desde {FormatoFecha.Antiguedad(desde)}.",
                momento, true, 4);
        }

        // Un sensor que marca siempre lo mismo hace que la temperatura mostrada no sea confiable.
        if (d.SensorSinVariacionDesde is { } fijo)
        {
            return new("Revisar sensor", "sen", "advertencia",
                $"El sensor marca exactamente el mismo valor desde {FormatoFecha.Antiguedad(fijo)}: puede estar dañado. Verifique con un termómetro.",
                momento, true, 4);
        }

        if (d.AlertaActiva is { Nivel: NivelAlertaDto.Advertencia } adv)
        {
            var sentido = adv.Tipo == TipoAlertaDto.TemperaturaAlta ? "alta" : "baja";
            var accion = Antiguedad(d) is { } hace
                ? $"Último estado conocido: temperatura {sentido}. Sin lecturas desde {hace}: revise el equipo."
                : $"Revise el termostato. Temperatura {sentido} desde {FormatoFecha.Antiguedad(adv.Inicio)}.";
            return new("Advertencia", "adv", "advertencia", accion, momento, false, 3);
        }

        return d.Estado switch
        {
            EstadoLectura.SinDatos => new("Sin lecturas", "sen", "sin-senal",
                "Este equipo todavía no ha enviado datos.", momento, true, 1),
            EstadoLectura.Desactualizada => new("Sin datos recientes", "sen", "reloj",
                "No llegan lecturas recientes del dispositivo.", momento, true, 2),
            _ => new("Normal", "ok", "ok",
                "Todo en orden. No se requiere ninguna acción.", momento, false, 0)
        };
    }
}
