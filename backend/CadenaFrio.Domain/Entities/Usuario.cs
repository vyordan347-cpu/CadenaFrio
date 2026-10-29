using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Entities;

// Persona del personal de la posta con acceso al sistema (RF03, RF19).
public class Usuario
{
    public int Id { get; set; }

    // Nombre visible, por ejemplo "Rosa Quispe". Se registra en los cambios de rango y revisiones.
    public string NombreCompleto { get; set; } = string.Empty;

    // Nombre con el que inicia sesión. Se guarda en minúsculas y es único.
    public string NombreUsuario { get; set; } = string.Empty;

    // Contraseña cifrada con PasswordHasher. Nunca se guarda en texto plano (RNF05).
    public string ContrasenaHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    // Permite quitar el acceso a alguien sin borrar su rastro en el historial.
    public bool Activo { get; set; } = true;

    // Verdadero cuando tiene una contraseña temporal (creada o restablecida por el
    // administrador): debe cambiarla antes de usar el sistema.
    public bool DebeCambiarContrasena { get; set; }

    // Valor aleatorio que viaja en la sesión. Si cambia (al desactivar al usuario, cambiar
    // su rol o su contraseña), todas sus sesiones abiertas dejan de ser válidas al instante.
    public string SelloSeguridad { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // ---------- Avisos (Fase 2) ----------

    // Correo para las copias de las alertas importantes.
    public string? Correo { get; set; }

    // Recibe el aviso inmediato de rupturas, pérdidas de señal y sensores dañados.
    public bool RecibeAlertas { get; set; }

    // Recibe el aviso de escalamiento cuando nadie atiende una alerta a tiempo
    // (por ejemplo, el jefe del establecimiento).
    public bool EsContactoEscalamiento { get; set; }

    // Chat de Telegram vinculado. Nulo si no vinculó su cuenta.
    public long? TelegramChatId { get; set; }

    // Código temporal para vincular Telegram: se envía al bot con /start.
    public string? CodigoVinculacionTelegram { get; set; }

    // Invalida todas las sesiones abiertas del usuario.
    public void RenovarSello() => SelloSeguridad = Guid.NewGuid().ToString("N");
}
