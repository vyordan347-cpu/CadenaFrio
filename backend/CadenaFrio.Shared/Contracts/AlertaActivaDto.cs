using System.Text.Json.Serialization;

namespace CadenaFrio.Shared.Contracts;

// Dirección de la alerta. Se envía como texto ("TemperaturaAlta").
[JsonConverter(typeof(JsonStringEnumConverter<TipoAlertaDto>))]
public enum TipoAlertaDto
{
    TemperaturaAlta = 1,
    TemperaturaBaja = 2
}

// Gravedad de la alerta. Se envía como texto ("Ruptura").
[JsonConverter(typeof(JsonStringEnumConverter<NivelAlertaDto>))]
public enum NivelAlertaDto
{
    Advertencia = 1,
    Ruptura = 2
}

// Alerta que sigue activa en un equipo (HU03).
public class AlertaActivaDto
{
    public TipoAlertaDto Tipo { get; set; }
    public NivelAlertaDto Nivel { get; set; }

    // Desde cuándo está activa (UTC).
    public DateTime Inicio { get; set; }

    // Desde cuándo hay ruptura (UTC). Nulo si sigue en advertencia.
    public DateTime? FechaRuptura { get; set; }

    // Temperatura más alejada del rango durante el episodio.
    public double ValorExtremo { get; set; }
}
