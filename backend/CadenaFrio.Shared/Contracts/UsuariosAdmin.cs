using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Un usuario en la lista del panel de administrador (RF19).
public class UsuarioAdminDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public bool DebeCambiarContrasena { get; set; }
    public DateTime FechaRegistro { get; set; }

    // Avisos
    public string? Correo { get; set; }
    public bool RecibeAlertas { get; set; }
    public bool EsContactoEscalamiento { get; set; }
    public bool TelegramVinculado { get; set; }
}

// Datos para crear un usuario. La contraseña la genera el sistema.
public class CrearUsuarioRequest
{
    [Required(ErrorMessage = "Ingrese el nombre completo.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    public string? NombreCompleto { get; set; }

    [Required(ErrorMessage = "Ingrese un nombre de usuario.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 50 caracteres.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Use solo letras sin tildes, números, punto, guion o guion bajo. Sin espacios.")]
    public string? NombreUsuario { get; set; }

    [Required(ErrorMessage = "Elija un rol.")]
    public string? Rol { get; set; }
}

// Cambio de rol de un usuario.
public class CambiarRolRequest
{
    [Required(ErrorMessage = "Elija un rol.")]
    public string? Rol { get; set; }
}

// Contraseña temporal recién generada. Solo se envía una vez.
public class ContrasenaTemporalDto
{
    public UsuarioAdminDto Usuario { get; set; } = new();
    public string ContrasenaTemporal { get; set; } = string.Empty;
}
