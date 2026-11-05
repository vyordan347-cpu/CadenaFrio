using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Entities;

// Una línea de la bitácora de auditoría: quién hizo qué y cuándo.
// Solo se agregan registros: no hay forma de editarlos ni borrarlos desde la aplicación.
public class RegistroAuditoria
{
    public int Id { get; set; }

    // Cuándo ocurrió (UTC).
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    // Nombre completo de quien lo hizo, tomado de la sesión.
    // En un intento fallido de inicio de sesión, el nombre de usuario que se escribió.
    public string Usuario { get; set; } = string.Empty;

    public CategoriaAuditoria Categoria { get; set; }

    // Qué hizo, en pocas palabras, por ejemplo "Revisó una ruptura".
    public string Accion { get; set; } = string.Empty;

    // Sobre qué y con qué datos, por ejemplo "Refrigeradora principal: Apagón".
    public string? Detalle { get; set; }
}
