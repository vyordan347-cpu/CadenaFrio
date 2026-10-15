using System.Text.Json.Serialization;
 
namespace CadenaFrio.Shared.Contracts;
 
// Estado de la última lectura de un equipo.
// Se envía como texto ("Actualizada") en lugar de número para que sea legible.
[JsonConverter(typeof(JsonStringEnumConverter<EstadoLectura>))]
public enum EstadoLectura
{
    SinDatos = 0,       // El equipo nunca envió lecturas.
    Actualizada = 1,    // La última lectura es reciente.
    Desactualizada = 2  // No llegan lecturas desde hace más de 5 minutos.
}
 
// Lo que la web necesita para mostrar la temperatura actual de un equipo (HU01).
public class TemperaturaActualDto
{
    public int EquipoId { get; set; }
 
    public string NombreEquipo { get; set; } = string.Empty;
 
    // Nulos cuando el equipo todavía no tiene lecturas.
    public double? Temperatura { get; set; }
    public DateTime? FechaMedicion { get; set; }
 
    public EstadoLectura Estado { get; set; }
}
 