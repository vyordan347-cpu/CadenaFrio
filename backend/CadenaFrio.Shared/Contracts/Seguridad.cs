namespace CadenaFrio.Shared.Contracts;

// Nombres de los roles. Coinciden con el enum RolUsuario del dominio.
// Están en Shared para que la API y la web usen exactamente los mismos textos.
public static class Roles
{
    public const string Enfermera = "Enfermera";
    public const string ResponsableInmunizaciones = "ResponsableInmunizaciones";
    public const string Administrador = "Administrador";
}

// Políticas de autorización: reglas con nombre que agrupan permisos.
public static class Politicas
{
    // Solo personal capacitado puede cambiar los rangos (HU02).
    public const string PuedeConfigurarRangos = "PuedeConfigurarRangos";
}

// Datos adicionales que viajan en la sesión del usuario.
public static class ClaimTiposCadenaFrio
{
    public const string NombreCompleto = "nombre_completo";
}
