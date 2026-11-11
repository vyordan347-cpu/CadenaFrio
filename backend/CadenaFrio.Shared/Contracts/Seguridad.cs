namespace CadenaFrio.Shared.Contracts;

// Nombres de los roles. Coinciden con el enum RolUsuario del dominio.
// Están en Shared para que la API y la web usen exactamente los mismos textos.
// Los nombres internos no cambian (se guardan en la sesión); los visibles siguen la NTS 242.
public static class Roles
{
    // Todos los roles, en orden, para listas desplegables.
    public static readonly string[] Todos = { Enfermera, ResponsableInmunizaciones, MedicoJefe, Administrador };

    // Nombre legible de cada rol, como lo nombra la NTS 242.
    public static string Nombre(string rol) => rol switch
    {
        Enfermera => "Técnico de enfermería",
        ResponsableInmunizaciones => "Responsable de inmunizaciones",
        MedicoJefe => "Médico jefe del establecimiento",
        Administrador => "Administrador",
        _ => rol
    };

    public const string Enfermera = "Enfermera";
    public const string ResponsableInmunizaciones = "ResponsableInmunizaciones";
    public const string MedicoJefe = "MedicoJefe";
    public const string Administrador = "Administrador";
}

// Políticas de autorización: reglas con nombre que agrupan permisos. Se definen una sola vez
// aquí (Configurar) y las usan la API, que es la seguridad real, y la web, que solo oculta
// lo que el usuario no puede usar. El administrador puede hacer todo.
public static class Politicas
{
    // Rangos de conservación: asignar vacunas y ajuste manual. Responsable de inmunizaciones (5.1.43).
    public const string PuedeConfigurarRangos = "PuedeConfigurarRangos";

    // Revisión de rupturas con el Anexo 13: la notificación la hace el jefe del establecimiento
    // a través del responsable de inmunizaciones (6.10.2).
    public const string PuedeRevisarRupturas = "PuedeRevisarRupturas";

    // Roles permitidos en cada política. Es la matriz de permisos del sistema.
    public static readonly IReadOnlyDictionary<string, string[]> RolesPermitidos = new Dictionary<string, string[]>
    {
        [PuedeConfigurarRangos] = new[] { Roles.ResponsableInmunizaciones, Roles.Administrador },
        [PuedeRevisarRupturas] = new[] { Roles.ResponsableInmunizaciones, Roles.MedicoJefe, Roles.Administrador },
        [EsAdministrador] = new[] { Roles.Administrador }
    };

    // Registra todas las políticas. Lo llaman Program.cs de la API y de la web.
    public static void Configurar(Microsoft.AspNetCore.Authorization.AuthorizationOptions opciones)
    {
        foreach (var (politica, roles) in RolesPermitidos)
        {
            opciones.AddPolicy(politica, p => p.RequireRole(roles));
        }
    }

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
