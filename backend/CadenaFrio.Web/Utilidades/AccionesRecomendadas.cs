using CadenaFrio.Shared.Contracts;

namespace CadenaFrio.Web.Utilidades;

// Una acción para el personal: qué hacer y, si corresponde, dónde hacerlo.
public record AccionRecomendada(string Icono, string Texto, string? Enlace = null, string? TextoEnlace = null);

// "Qué hacer" en el Historial.
//
// IMPORTANTE: estos textos deben ser validados por la persona responsable de inmunizaciones
// del establecimiento. Solo repiten lo que el sistema ya aplica según la NTS N° 242-MINSA/DGIESP-2026
// (revisión con el Anexo 13, plazo de 24 h del numeral 6.10.2.1) y lo que ya dice el panel
// (revisar el equipo y comparar con el termómetro). No agregan protocolos clínicos nuevos.
// Para cambiar un texto, se cambia aquí y en ningún otro lugar.
public static class AccionesRecomendadas
{
    public static List<AccionRecomendada> Para(HistorialDto h, int cantidadHuecos, bool informacionAntigua)
    {
        var acciones = new List<AccionRecomendada>();
        var detalle = $"equipos/{h.EquipoId}";

        var rupturasSinRevisar = h.Alertas.Count(a => a.Nivel == NivelAlertaDto.Ruptura && a.Tipo != TipoAlertaDto.SinSenal
                                                      && a.RequiereRevision && a.AtendidaPor is null);
        var silenciosSinRevisar = h.Alertas.Count(a => a.Tipo == TipoAlertaDto.SinSenal && a.RequiereRevision && a.AtendidaPor is null);

        if (rupturasSinRevisar > 0)
        {
            acciones.Add(new("ruptura",
                "Registre la revisión de la ruptura con el formato del Anexo 13: la causa, cómo se restableció la temperatura " +
                "y si se inmovilizaron vacunas. La notificación debe hacerse dentro de las 24 horas siguientes a la ruptura " +
                "(NTS 242, numeral 6.10.2.1).",
                detalle, rupturasSinRevisar == 1 ? "Revisar la ruptura" : $"Revisar las {rupturasSinRevisar} rupturas"));
        }

        if (silenciosSinRevisar > 0)
        {
            acciones.Add(new("sin-senal",
                "Registre la revisión del periodo sin señal: anote qué pasó (por ejemplo, un corte de luz o el equipo desconectado).",
                detalle, silenciosSinRevisar == 1 ? "Revisar el periodo sin señal" : $"Revisar los {silenciosSinRevisar} periodos sin señal"));
        }

        if (cantidadHuecos > 0 || informacionAntigua)
        {
            acciones.Add(new("temperatura",
                "Revise que el equipo esté encendido y que el dispositivo de medición esté conectado. " +
                "Compare con el termómetro del equipo."));
        }

        // Siempre, al final: el sistema informa, no reemplaza el criterio del personal responsable.
        acciones.Add(new("info", "Ante cualquier duda sobre el estado de las vacunas, consulte al responsable de inmunizaciones."));
        return acciones;
    }
}
