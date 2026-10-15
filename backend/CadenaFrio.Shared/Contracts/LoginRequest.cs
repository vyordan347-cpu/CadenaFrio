using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Datos de la pantalla de inicio de sesión.
public class LoginRequest
{
    [Required(ErrorMessage = "Ingrese su usuario.")]
    public string? NombreUsuario { get; set; }

    [Required(ErrorMessage = "Ingrese su contraseña.")]
    public string? Contrasena { get; set; }
}
