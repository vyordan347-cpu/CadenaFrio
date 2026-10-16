using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Datos que envía el dispositivo en cada lectura.
// La clave del dispositivo no va aquí, sino en el encabezado X-Clave-Dispositivo.
public class RegistrarLecturaRequest
{
    [Required(ErrorMessage = "La temperatura es obligatoria.")]
    [Range(LimitesFisicos.TemperaturaMinima, LimitesFisicos.TemperaturaMaxima,
        ErrorMessage = "La temperatura debe estar entre -50 °C y 60 °C.")]
    public double? Temperatura { get; set; }

    // Momento en que el dispositivo midió, en UTC. Es opcional: si no se envía,
    // se usa la hora de llegada al servidor.
    public DateTime? FechaMedicion { get; set; }
}
