using System.Security.Claims;
using CadenaFrio.Api.Seguridad;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Inicio y cierre de sesión, y cambio de contraseña (RF03).
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // El mismo mensaje para usuario inexistente y contraseña incorrecta:
    // así no se revela qué nombres de usuario existen.
    private const string MensajeCredencialesInvalidas = "Usuario o contraseña incorrectos.";

    private readonly CadenaFrioDbContext _db;
    private readonly IPasswordHasher<Usuario> _cifrador;
    private readonly Bitacora _bitacora;

    public AuthController(CadenaFrioDbContext db, IPasswordHasher<Usuario> cifrador, Bitacora bitacora)
    {
        _db = db;
        _cifrador = cifrador;
        _bitacora = bitacora;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<UsuarioActualDto>> IniciarSesion(LoginRequest solicitud)
    {
        var nombreUsuario = solicitud.NombreUsuario!.Trim().ToLowerInvariant();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

        // Los intentos fallidos se anotan con el nombre que se escribió (nunca la contraseña).
        // El motivo solo lo ve el administrador en la bitácora, no quien intenta entrar.
        if (usuario is null || !usuario.Activo)
        {
            await _bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Sesion, "Intento fallido de inicio de sesión",
                nombreUsuario, usuario is null ? "El usuario no existe." : "El usuario está desactivado.");
            return Unauthorized(new { mensaje = MensajeCredencialesInvalidas });
        }

        var resultado = _cifrador.VerifyHashedPassword(usuario, usuario.ContrasenaHash, solicitud.Contrasena!);
        if (resultado == PasswordVerificationResult.Failed)
        {
            await _bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Sesion, "Intento fallido de inicio de sesión",
                nombreUsuario, "Contraseña incorrecta.");
            return Unauthorized(new { mensaje = MensajeCredencialesInvalidas });
        }

        // Si el algoritmo de cifrado se actualizó, se vuelve a cifrar con el nuevo.
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.ContrasenaHash = _cifrador.HashPassword(usuario, solicitud.Contrasena!);
            await _db.SaveChangesAsync();
        }

        await _bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Sesion, "Inició sesión",
            usuario.NombreCompleto, $"Usuario: {usuario.NombreUsuario}");
        await SesionUsuario.IniciarAsync(HttpContext, usuario);
        return Ok(ConvertirADto(usuario));
    }

    // POST /api/auth/logout
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> CerrarSesion()
    {
        await _bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Sesion, "Cerró sesión", UsuarioActual);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    // GET /api/auth/yo
    // La web lo consulta para saber si hay sesión, con qué rol y si debe cambiar la contraseña.
    [Authorize]
    [HttpGet("yo")]
    public ActionResult<UsuarioActualDto> ObtenerUsuarioActual() => Ok(new UsuarioActualDto
    {
        NombreUsuario = User.Identity!.Name!,
        NombreCompleto = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity.Name!,
        Rol = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
        DebeCambiarContrasena = User.HasClaim(ClaimTiposCadenaFrio.DebeCambiarContrasena, "true")
    });

    // POST /api/auth/cambiar-contrasena
    // El propio usuario cambia su contraseña. Obligatorio si tiene una temporal.
    [Authorize]
    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaRequest solicitud)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await _db.Usuarios.FirstAsync(u => u.Id == id);

        var actualCorrecta = _cifrador.VerifyHashedPassword(usuario, usuario.ContrasenaHash, solicitud.ContrasenaActual!)
                             != PasswordVerificationResult.Failed;
        if (!actualCorrecta)
        {
            return BadRequest(new { mensaje = "La contraseña actual no es correcta." });
        }

        _bitacora.Registrar(CategoriaAuditoria.Sesion, "Cambió su contraseña", usuario.NombreCompleto,
            usuario.DebeCambiarContrasena ? "Reemplazó la contraseña temporal." : null);
        usuario.ContrasenaHash = _cifrador.HashPassword(usuario, solicitud.ContrasenaNueva!);
        usuario.DebeCambiarContrasena = false;

        // Nuevo sello: se cierran las demás sesiones abiertas de este usuario (por ejemplo,
        // en otra PC). La sesión actual se renueva con el sello nuevo para no expulsarlo.
        usuario.RenovarSello();
        await _db.SaveChangesAsync();
        await SesionUsuario.IniciarAsync(HttpContext, usuario);

        return NoContent();
    }

    private string UsuarioActual =>
        User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;

    private static UsuarioActualDto ConvertirADto(Usuario usuario) => new()
    {
        NombreUsuario = usuario.NombreUsuario,
        NombreCompleto = usuario.NombreCompleto,
        Rol = usuario.Rol.ToString(),
        DebeCambiarContrasena = usuario.DebeCambiarContrasena
    };
}
