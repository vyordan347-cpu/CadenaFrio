namespace CadenaFrio.Domain.Entities;
 
// Rango de temperatura permitido para un equipo (HU02).
// Cada cambio crea una fila nueva: el rango vigente es el más reciente
// y los anteriores quedan como historial de cambios (RF05).
public class RangoTemperatura
{
    // Rango usado cuando un equipo no tiene ninguno configurado (RF04).
    public const double MinimaPorDefecto = 2.0;
    public const double MaximaPorDefecto = 8.0;
 
    public int Id { get; set; }
 
    public int EquipoId { get; set; }
    public Equipo Equipo { get; set; } = null!;
 
    // Límites en °C.
    public double TemperaturaMinima { get; set; }
    public double TemperaturaMaxima { get; set; }
 
    // Cuándo se definió este rango (UTC).
    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;
 
    // Quién hizo el cambio. Es texto mientras no exista el inicio de sesión;
    // luego se reemplazará por una referencia al usuario.
    public string ModificadoPor { get; set; } = string.Empty;
}