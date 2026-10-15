namespace CadenaFrio.Shared.Contracts;
 
// Rango de temperatura de un equipo, tal como lo ve la web (HU02).
public class RangoTemperaturaDto
{
    public int EquipoId { get; set; }
 
    public double TemperaturaMinima { get; set; }
    public double TemperaturaMaxima { get; set; }
 
    // Nulos cuando el rango es el de por defecto (nadie lo configuró).
    public DateTime? FechaCambio { get; set; }
    public string? ModificadoPor { get; set; }
 
    // Indica si es el rango automático de +2 °C a +8 °C.
    public bool EsPorDefecto { get; set; }
}
 