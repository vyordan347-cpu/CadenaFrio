namespace CadenaFrio.Infrastructure.Configuracion;

// Resumen diario por Telegram, configurable en appsettings.json (sección "ResumenDiario").
public class OpcionesResumenDiario
{
    // false: no se envía el resumen automático (el comando /resumen del bot sigue funcionando).
    public bool Activo { get; set; } = true;

    // Hora de Perú a la que se envía el resumen del día anterior (0 a 23).
    // Si el servidor está apagado a esa hora, ese día no se envía.
    public int Hora { get; set; } = 7;
}
