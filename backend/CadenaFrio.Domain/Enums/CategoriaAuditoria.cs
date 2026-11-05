namespace CadenaFrio.Domain.Enums;

// Agrupa los registros de la bitácora para poder filtrarlos.
// Números explícitos: los valores guardados no cambian si se agrega una categoría.
public enum CategoriaAuditoria
{
    Sesion = 1,          // Inicios y cierres de sesión, intentos fallidos, cambios de contraseña.
    Rangos = 2,          // Vacunas asignadas a un equipo y rangos de temperatura.
    Alertas = 3,         // Revisiones y "Lo estoy atendiendo".
    Administracion = 4   // Usuarios, equipos y vacunas.
}
