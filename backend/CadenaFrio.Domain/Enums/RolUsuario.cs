namespace CadenaFrio.Domain.Enums;

// Roles del personal. Cada rol determina qué puede hacer en el sistema.
public enum RolUsuario
{
    Enfermera = 1,                  // Ve temperatura y alertas.
    ResponsableInmunizaciones = 2,  // Además, configura los rangos.
    Administrador = 3               // Además, gestiona equipos y usuarios (RF19).
}
