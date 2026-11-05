namespace CadenaFrio.Infrastructure.Configuracion;

// Respaldos de la base de datos, configurables en appsettings.json (sección "Respaldos").
public class OpcionesRespaldo
{
    // Carpeta donde se guardan. Si es relativa, se toma desde la carpeta de la API
    // (la misma de cadenafrio.db). Program.cs la convierte en ruta completa.
    public string Carpeta { get; set; } = "respaldos";

    // Hora de Perú a partir de la cual se hace el respaldo automático del día (0 a 23).
    // Si la PC estaba apagada a esa hora, se hace en cuanto se encienda.
    public int HoraDiaria { get; set; } = 3;

    // Cuántos respaldos se conservan de cada tipo (automáticos y manuales, por separado).
    public int Conservar { get; set; } = 7;
}
