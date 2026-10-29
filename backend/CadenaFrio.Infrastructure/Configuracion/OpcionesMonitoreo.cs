namespace CadenaFrio.Infrastructure.Configuracion;

// Tiempos del monitoreo, configurables en appsettings.json (sección "Monitoreo").
// Están en un solo lugar para que el vigilante y la pantalla usen los mismos valores.
public class OpcionesMonitoreo
{
    // Minutos sin recibir datos para considerar que el dispositivo perdió la señal.
    // El dispositivo envía una lectura por minuto, así que 5 minutos ya es anormal.
    public int MinutosSinSenal { get; set; } = 5;

    // Minutos de silencio a partir de los cuales se exige revisión del personal:
    // alguien debe verificar físicamente las vacunas.
    public int MinutosSinSenalParaRevision { get; set; } = 30;

    // Cada cuántos segundos el vigilante revisa los equipos.
    public int SegundosEntreRevisiones { get; set; } = 60;

    // Minutos con exactamente la misma temperatura para sospechar que el sensor está dañado.
    // Un sensor de precisión (DS18B20, 0.0625 °C) siempre muestra pequeñas variaciones.
    public int MinutosSensorSinVariacion { get; set; } = 120;

    public TimeSpan LimiteSinSenal => TimeSpan.FromMinutes(MinutosSinSenal);
    public TimeSpan LimiteSinSenalParaRevision => TimeSpan.FromMinutes(MinutosSinSenalParaRevision);
    public TimeSpan IntervaloRevision => TimeSpan.FromSeconds(SegundosEntreRevisiones);
    public TimeSpan LimiteSensorSinVariacion => TimeSpan.FromMinutes(MinutosSensorSinVariacion);
}
