using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Valores;

// Una opción de las listas del Anexo N° 13.
// - Codigo: lo que se guarda en la base de datos. No cambia aunque cambie el texto.
// - Grupo: encabezado bajo el que aparece en el formato (por ejemplo, la falla de energía).
// - RequiereDetalle: "Otro" y "Otra causa" obligan a escribir cuál fue.
public record OpcionAnexo13(string Codigo, string Descripcion, string? Grupo = null, bool RequiereDetalle = false);

// Listas oficiales del "Formato de notificación de ruptura de cadena de frío"
// (Anexo N° 13 de la NTS N° 242-MINSA/DGIESP-2026, numeral 6.10.2.1).
// Son fijas porque las define la norma: no se editan desde la aplicación.
public static class CatalogoAnexo13
{
    // Plazo para notificar la ruptura, contado desde que se detecta.
    public static readonly TimeSpan PlazoNotificacion = TimeSpan.FromHours(24);

    private const string GrupoEnergia = "Falla en el suministro de energía";

    // Causa de la ruptura en el equipo de refrigeración.
    public static IReadOnlyList<OpcionAnexo13> CausasRefrigerador { get; } = new[]
    {
        new OpcionAnexo13("equipo-malogrado", "Equipo malogrado, no funciona"),
        new OpcionAnexo13("calibracion-termostato", "Calibración inadecuada, movieron el termostato"),
        new OpcionAnexo13("puerta-mal-cerrada", "No cerró adecuadamente la puerta"),
        new OpcionAnexo13("energia-desconexion", "Desconexión del enchufe y/o estabilizador", GrupoEnergia),
        new OpcionAnexo13("energia-corto-circuito", "Corto circuito", GrupoEnergia),
        new OpcionAnexo13("energia-apagon", "Apagón", GrupoEnergia),
        new OpcionAnexo13("energia-bateria-agotada", "Batería agotada", GrupoEnergia),
        new OpcionAnexo13("energia-otro", "Otro", GrupoEnergia, RequiereDetalle: true),
        new OpcionAnexo13("falla-alarma", "Falla del sistema de alarma"),
        new OpcionAnexo13("no-controlo-temperatura", "No controló temperatura del refrigerador"),
        new OpcionAnexo13("otra", "Otra causa", RequiereDetalle: true)
    };

    // Causa de la ruptura en el complemento de cadena de frío (termo o caja transportadora).
    public static IReadOnlyList<OpcionAnexo13> CausasTermo { get; } = new[]
    {
        new OpcionAnexo13("no-cambio-pf", "No cambió paquetes fríos oportunamente"),
        new OpcionAnexo13("no-registro-cambio-pf", "No registró cambio de paquetes fríos"),
        new OpcionAnexo13("sin-registro-temperatura", "No tiene registro de temperatura actualizado"),
        new OpcionAnexo13("uso-pf-euteticos", "Uso de paquetes eutécticos"),
        new OpcionAnexo13("otra", "Otra causa", RequiereDetalle: true)
    };

    // ¿Cómo restableció la cadena de frío?
    public static IReadOnlyList<OpcionAnexo13> FormasRestablecimiento { get; } = new[]
    {
        new OpcionAnexo13("traslado-termo", "Traslado de vacunas a termo o caja transportadora (plan de contingencia)"),
        new OpcionAnexo13("traslado-otro-eess", "Traslado de vacunas a otro establecimiento con refrigerador"),
        new OpcionAnexo13("otros", "Otros", RequiereDetalle: true)
    };

    // El Anexo 13 se llena solo para rupturas de temperatura. Un silencio prolongado no es
    // una ruptura confirmada, y una advertencia no exige revisión: ambos usan texto libre.
    public static bool Aplica(TipoAlerta tipo, NivelAlerta nivel) =>
        nivel == NivelAlerta.Ruptura && (tipo is TipoAlerta.TemperaturaAlta or TipoAlerta.TemperaturaBaja);

    // El formato separa las causas según dónde ocurrió la ruptura.
    public static IReadOnlyList<OpcionAnexo13> CausasPara(TipoEquipo tipo) =>
        tipo == TipoEquipo.Termo ? CausasTermo : CausasRefrigerador;

    public static OpcionAnexo13? BuscarCausa(TipoEquipo tipo, string? codigo) =>
        CausasPara(tipo).FirstOrDefault(o => o.Codigo == codigo);

    public static OpcionAnexo13? BuscarFormaRestablecimiento(string? codigo) =>
        FormasRestablecimiento.FirstOrDefault(o => o.Codigo == codigo);

    // Revisa que la notificación esté completa y use solo opciones del formato.
    // Devuelve el mensaje para el usuario, o null si todo está correcto.
    public static string? Validar(
        TipoEquipo tipo,
        string? causa, string? detalleCausa,
        string? formaRestablecimiento, string? detalleRestablecimiento,
        bool? vacunasInmovilizadas)
    {
        var opcionCausa = BuscarCausa(tipo, causa);
        if (opcionCausa is null)
        {
            return "Seleccione una causa de la ruptura de la lista.";
        }

        if (opcionCausa.RequiereDetalle && string.IsNullOrWhiteSpace(detalleCausa))
        {
            return "Especifique cuál fue la causa de la ruptura.";
        }

        var opcionForma = BuscarFormaRestablecimiento(formaRestablecimiento);
        if (opcionForma is null)
        {
            return "Seleccione cómo se restableció la cadena de frío.";
        }

        if (opcionForma.RequiereDetalle && string.IsNullOrWhiteSpace(detalleRestablecimiento))
        {
            return "Especifique cómo se restableció la cadena de frío.";
        }

        if (vacunasInmovilizadas is null)
        {
            return "Indique si las vacunas están inmovilizadas.";
        }

        return null;
    }
}
