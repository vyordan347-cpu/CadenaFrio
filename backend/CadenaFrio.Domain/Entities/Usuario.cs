using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Entities;

// Persona del personal de la posta con acceso al sistema (RF03).
public class Usuario
{
    public int Id { get; set; }

    // Nombre visible, por ejemplo "Rosa Quispe". Se registra en los cambios de rango.
    public string NombreCompleto { get; set; } = string.Empty;

    // Nombre con el que inicia sesión. Se guarda en minúsculas y es único.
    public string NombreUsuario { get; set; } = string.Empty;

    // Contraseña cifrada con PasswordHasher. Nunca se guarda en texto plano (RNF05).
    public string ContrasenaHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    // Permite quitar el acceso a alguien sin borrar su rastro en el historial.
    public bool Activo { get; set; } = true;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
