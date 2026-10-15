using System.Text.Json.Serialization;

namespace CadenaFrio.Shared.Contracts;

// Tipo de alerta tal como lo ve la web. Se envía como texto ("TemperaturaAlta").
[JsonConverter(typeof(JsonStringEnumConverter<TipoAlertaDto>))]
public enum TipoAlertaDto
{
    TemperaturaAlta = 1,
    TemperaturaBaja = 2
}

// Alerta que sigue activa en un equipo (HU03).
public class AlertaActivaDto
{
    public TipoAlertaDto Tipo { get; set; }

    // Desde cuándo está activa (UTC).
    public DateTime Inicio { get; set; }

    // Temperatura más alejada del rango durante la alerta.
    public double ValorExtremo { get; set; }

    // Rango vigente cuando se abrió la alerta.
    public double RangoMinimo { get; set; }
    public double RangoMaximo { get; set; }
}
