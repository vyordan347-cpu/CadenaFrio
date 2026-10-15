using System.ComponentModel.DataAnnotations;
 
namespace CadenaFrio.Shared.Contracts;
 
// Datos que envía el dispositivo en cada lectura.
// La clave del dispositivo no va aquí, sino en el encabezado X-Clave-Dispositivo.
public class RegistrarLecturaRequest
{
    // El rango descarta valores imposibles que el sensor DS18B20 entrega cuando falla:
    // -127 °C (sensor desconectado) y 85 °C (error de lectura al encenderse).
    [Required(ErrorMessage = "La temperatura es obligatoria.")]
    [Range(-50.0, 60.0, ErrorMessage = "La temperatura debe estar entre -50 °C y 60 °C.")]
    public double? Temperatura { get; set; }
 
    // Momento en que el dispositivo midió, en UTC. Es opcional: si no se envía,
    // se usa la hora de llegada al servidor. Es necesaria al sincronizar
    // lecturas tomadas sin conexión (HU06).
    public DateTime? FechaMedicion { get; set; }
}
 