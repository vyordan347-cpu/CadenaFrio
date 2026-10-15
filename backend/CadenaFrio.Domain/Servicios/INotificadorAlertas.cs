using CadenaFrio.Domain.Entities;

namespace CadenaFrio.Domain.Servicios;

// Canal por el que se avisa de las alertas.
// El motor de alertas solo conoce esta interfaz, no el canal concreto:
// para usar WhatsApp, correo o la app Android, se crea otra implementación
// y se cambia una línea en Program.cs, sin tocar el motor.
public interface INotificadorAlertas
{
    Task AlertaAbiertaAsync(Alerta alerta, string nombreEquipo);

    // El aviso más urgente: una advertencia pasó a ruptura de la cadena de frío.
    Task AlertaEscaladaAsync(Alerta alerta, string nombreEquipo);

    Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo);
}
