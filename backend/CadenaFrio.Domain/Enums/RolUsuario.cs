namespace CadenaFrio.Domain.Enums;

// Roles del personal, según la NTS N° 242-MINSA/DGIESP-2026. Cada rol determina qué puede
// hacer en el sistema. Los valores se guardan como número en la base: no se cambian.
public enum RolUsuario
{
    // Técnico de enfermería o de farmacia: registro diario delegado (6.3.1.3). Ve temperatura y alertas.
    Enfermera = 1,

    // Profesional de enfermería responsable de inmunizaciones y de cadena de frío (5.1.43, 5.1.44):
    // configura los rangos y registra la revisión de rupturas (Anexo 13).
    ResponsableInmunizaciones = 2,

    // Soporte del sistema: gestiona equipos y usuarios (RF19) y puede hacer todo lo demás.
    Administrador = 3,

    // Médico jefe del establecimiento: notifica la ruptura a través del responsable de
    // inmunizaciones (6.10.2). Registra la revisión de rupturas; no configura rangos.
    MedicoJefe = 4
}
