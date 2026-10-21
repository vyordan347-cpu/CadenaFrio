using System.Security.Claims;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Seguridad;

// Todo lo relacionado con la sesión (cookie) de un usuario.
public static class SesionUsuario
{
    // Crea la sesión: los "claims" son los datos del usuario que viajan dentro de la cookie cifrada.
    public static Task IniciarAsync(HttpContext http, Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new(ClaimTiposCadenaFrio.NombreCompleto, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol.ToString()),
            new(ClaimTiposCadenaFrio.Sello, usuario.SelloSeguridad)
        };

        if (usuario.DebeCambiarContrasena)
        {
            claims.Add(new Claim(ClaimTiposCadenaFrio.DebeCambiarContrasena, "true"));
        }

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
    }

    // Se ejecuta en cada petición con sesión: comprueba que el usuario siga activo y que su
    // sello no haya cambiado. Así, desactivar a alguien o cambiar su rol cierra sus sesiones
    // al instante, en lugar de esperar a que la cookie expire (8 horas).
    public static async Task ValidarAsync(CookieValidatePrincipalContext contexto)
    {
        var id = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sello = contexto.Principal?.FindFirstValue(ClaimTiposCadenaFrio.Sello);

        var db = contexto.HttpContext.RequestServices.GetRequiredService<CadenaFrioDbContext>();
        var vigente = int.TryParse(id, out var usuarioId)
                      && await db.Usuarios.AnyAsync(u => u.Id == usuarioId && u.Activo && u.SelloSeguridad == sello);

        if (!vigente)
        {
            contexto.RejectPrincipal();
            await contexto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    // Middleware: mientras el usuario tenga una contraseña temporal, la API solo le permite
    // las rutas de sesión (/api/auth). Se aplica en el servidor, no solo en la pantalla.
    public static async Task ExigirCambioDeContrasenaAsync(HttpContext http, Func<Task> siguiente)
    {
        var debeCambiar = http.User.HasClaim(ClaimTiposCadenaFrio.DebeCambiarContrasena, "true");
        var ruta = http.Request.Path;

        if (debeCambiar && ruta.StartsWithSegments("/api") && !ruta.StartsWithSegments("/api/auth"))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            await http.Response.WriteAsJsonAsync(new { mensaje = "Debe cambiar su contraseña temporal antes de continuar." });
            return;
        }

        await siguiente();
    }
}
