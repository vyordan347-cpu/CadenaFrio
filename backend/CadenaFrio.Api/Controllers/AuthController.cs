using System.Security.Claims;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Inicio y cierre de sesión del personal (RF03).
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // El mismo mensaje para usuario inexistente y contraseña incorrecta:
    // así no se revela qué nombres de usuario existen.
    private const string MensajeCredencialesInvalidas = "Usuario o contraseña incorrectos.";

    private readonly CadenaFrioDbContext _db;
    private readonly IPasswordHasher<Usuario> _cifrador;

    public AuthController(CadenaFrioDbContext db, IPasswordHasher<Usuario> cifrador)
    {
        _db = db;
        _cifrador = cifrador;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<UsuarioActualDto>> IniciarSesion(LoginRequest solicitud)
    {
        var nombreUsuario = solicitud.NombreUsuario!.Trim().ToLowerInvariant();

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new { mensaje = MensajeCredencialesInvalidas });
        }

        var resultado = _cifrador.VerifyHashedPassword(usuario, usuario.ContrasenaHash, solicitud.Contrasena!);

        if (resultado == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { mensaje = MensajeCredencialesInvalidas });
        }

        // Si el algoritmo de cifrado se actualizó, se vuelve a cifrar con el nuevo.
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.ContrasenaHash = _cifrador.HashPassword(usuario, solicitud.Contrasena!);
            await _db.SaveChangesAsync();
        }

        // Los "claims" son los datos del usuario que viajan dentro de la cookie de sesión.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new(ClaimTiposCadenaFrio.NombreCompleto, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol.ToString())
        };

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Crea la cookie de sesión cifrada y la envía al navegador.
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));

        return Ok(new UsuarioActualDto
        {
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Rol = usuario.Rol.ToString()
        });
    }

    // POST /api/auth/logout
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> CerrarSesion()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    // GET /api/auth/yo
    // La web lo consulta para saber si hay sesión y con qué rol.
    [Authorize]
    [HttpGet("yo")]
    public ActionResult<UsuarioActualDto> ObtenerUsuarioActual() => Ok(new UsuarioActualDto
    {
        NombreUsuario = User.Identity!.Name!,
        NombreCompleto = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity.Name!,
        Rol = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty
    });
}
