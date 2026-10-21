namespace CadenaFrio.Shared.Contracts;

// Nombres de los roles. Coinciden con el enum RolUsuario del dominio.
// Están en Shared para que la API y la web usen exactamente los mismos textos.
public static class Roles
{
    // Todos los roles, en orden, para listas desplegables.
    public static readonly string[] Todos = { Enfermera, ResponsableInmunizaciones, Administrador };

    // Nombre legible de cada rol.
    public static string Nombre(string rol) => rol switch
    {
        Enfermera => "Enfermera",
        ResponsableInmunizaciones => "Responsable de inmunizaciones",
        Administrador => "Administrador",
        _ => rol
    };

    public const string Enfermera = "Enfermera";
    public const string ResponsableInmunizaciones = "ResponsableInmunizaciones";
    public const string Administrador = "Administrador";
}

// Políticas de autorización: reglas con nombre que agrupan permisos.
public static class Politicas
{
    // Solo personal capacitado puede cambiar los rangos (HU02).
    public const string PuedeConfigurarRangos = "PuedeConfigurarRangos";

    // Solo el administrador gestiona equipos y usuarios (RF19).
    public const string EsAdministrador = "EsAdministrador";
}

// Datos adicionales que viajan en la sesión del usuario.
public static class ClaimTiposCadenaFrio
{
    public const string NombreCompleto = "nombre_completo";

    // Sello de seguridad: si ya no coincide con el del usuario, la sesión se invalida.
    public const string Sello = "sello";

    // Presente cuando el usuario debe cambiar su contraseña temporal.
    public const string DebeCambiarContrasena = "debe_cambiar_contrasena";
}
