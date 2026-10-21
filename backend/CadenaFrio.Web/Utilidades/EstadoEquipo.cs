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

public static class EstadoEquipo
{
    // Por orden de gravedad: ruptura > sin señal > advertencia > sin datos recientes > normal.
    public static VistaEstado Calcular(TemperaturaActualDto d)
    {
        var momento = d.FechaMedicion is { } f
            ? $"Última lectura {FormatoFecha.Antiguedad(f)}"
            : "Sin lecturas todavía";

        if (d.AlertaActiva is { Nivel: NivelAlertaDto.Ruptura } rup)
        {
            var sentido = rup.Tipo == TipoAlertaDto.TemperaturaAlta ? "Sobre" : "Bajo";
            return new("Ruptura", "rup", "ruptura",
                $"Active el plan de contingencia. {sentido} el límite desde {FormatoFecha.Antiguedad(rup.FechaRuptura ?? rup.Inicio)}.",
                momento, false, 5);
        }

        if (d.SinSenalDesde is { } desde)
        {
            return new("Sin señal", "sen", "sin-senal",
                $"Posible corte de energía o falla de conexión. Sin datos desde {FormatoFecha.Antiguedad(desde)}.",
                momento, true, 4);
        }

        if (d.AlertaActiva is { Nivel: NivelAlertaDto.Advertencia } adv)
        {
            var sentido = adv.Tipo == TipoAlertaDto.TemperaturaAlta ? "alta" : "baja";
            return new("Advertencia", "adv", "advertencia",
                $"Revise el termostato. Temperatura {sentido} desde {FormatoFecha.Antiguedad(adv.Inicio)}.",
                momento, false, 3);
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
