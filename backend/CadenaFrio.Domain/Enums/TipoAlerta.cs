namespace CadenaFrio.Domain.Enums;

// Tipos de alerta que puede generar el sistema.
public enum TipoAlerta
{
    TemperaturaAlta = 1,
    TemperaturaBaja = 2,

    // El dispositivo dejó de enviar datos: posible corte de energía o falla de conexión (HU04, HU05).
    SinSenal = 3,

    // El sensor marca exactamente el mismo valor durante demasiado tiempo: puede estar dañado
    // o "congelado". Una temperatura real siempre varía un poco.
    SensorSinVariacion = 4
}
