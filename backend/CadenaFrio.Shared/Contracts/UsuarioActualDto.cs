namespace CadenaFrio.Shared.Contracts;

// Quién tiene la sesión iniciada. La web lo usa para decidir qué mostrar.
public class UsuarioActualDto
{
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;

    // Si tiene una contraseña temporal, la web lo lleva a cambiarla.
    public bool DebeCambiarContrasena { get; set; }
}
