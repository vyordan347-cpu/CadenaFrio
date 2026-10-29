using System.Security.Claims;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Gestión de usuarios desde el panel de administrador (RF19).
// Toda la clase exige el rol Administrador: los demás reciben 403.
[ApiController]
[Authorize(Policy = Politicas.EsAdministrador)]
[Route("api/admin/usuarios")]
public class AdminUsuariosController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;
    private readonly IPasswordHasher<Usuario> _cifrador;

    public AdminUsuariosController(CadenaFrioDbContext db, IPasswordHasher<Usuario> cifrador)
    {
        _db = db;
        _cifrador = cifrador;
    }

    private int IdActual => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/admin/usuarios
    [HttpGet]
    public async Task<ActionResult<List<UsuarioAdminDto>>> Listar()
    {
        var usuarios = await _db.Usuarios
            .AsNoTracking()
            .OrderByDescending(u => u.Activo)
            .ThenBy(u => u.NombreCompleto)
            .ToListAsync();

        return Ok(usuarios.Select(ConvertirADto).ToList());
    }

    // POST /api/admin/usuarios
    // Crea el usuario con una contraseña temporal, que se devuelve una sola vez.
    [HttpPost]
    public async Task<ActionResult<ContrasenaTemporalDto>> Crear(CrearUsuarioRequest solicitud)
    {
        if (!Enum.TryParse<RolUsuario>(solicitud.Rol, out var rol))
        {
            return BadRequest(new { mensaje = "Rol no válido." });
        }

        var nombreUsuario = solicitud.NombreUsuario!.Trim().ToLowerInvariant();
        if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario))
        {
            return Conflict(new { mensaje = $"Ya existe el usuario \"{nombreUsuario}\". Elija otro nombre." });
        }

        var temporal = ContrasenasTemporales.Generar();
        var usuario = new Usuario
        {
            NombreCompleto = solicitud.NombreCompleto!.Trim(),
            NombreUsuario = nombreUsuario,
            Rol = rol,
            Activo = true,
            DebeCambiarContrasena = true
        };
        usuario.ContrasenaHash = _cifrador.HashPassword(usuario, temporal);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return Ok(new ContrasenaTemporalDto { Usuario = ConvertirADto(usuario), ContrasenaTemporal = temporal });
    }

    // PUT /api/admin/usuarios/{id}/rol
    [HttpPut("{id:int}/rol")]
    public async Task<ActionResult<UsuarioAdminDto>> CambiarRol(int id, CambiarRolRequest solicitud)
    {
        if (!Enum.TryParse<RolUsuario>(solicitud.Rol, out var rol))
        {
            return BadRequest(new { mensaje = "Rol no válido." });
        }

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        if (id == IdActual)
        {
            return BadRequest(new { mensaje = "No puede cambiar su propio rol." });
        }

        if (usuario.Rol == RolUsuario.Administrador && rol != RolUsuario.Administrador && !await HayOtroAdministradorAsync(id))
        {
            return BadRequest(new { mensaje = "Debe quedar al menos un administrador activo." });
        }

        usuario.Rol = rol;
        // Cierra sus sesiones: al volver a entrar, tendrá los permisos del rol nuevo.
        usuario.RenovarSello();
        await _db.SaveChangesAsync();

        return Ok(ConvertirADto(usuario));
    }

    // POST /api/admin/usuarios/{id}/desactivar
    [HttpPost("{id:int}/desactivar")]
    public async Task<ActionResult<UsuarioAdminDto>> Desactivar(int id)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        if (id == IdActual)
        {
            return BadRequest(new { mensaje = "No puede desactivar su propia cuenta." });
        }

        if (usuario.Rol == RolUsuario.Administrador && !await HayOtroAdministradorAsync(id))
        {
            return BadRequest(new { mensaje = "Debe quedar al menos un administrador activo." });
        }

        usuario.Activo = false;
        // Cierra todas sus sesiones al instante.
        usuario.RenovarSello();
        await _db.SaveChangesAsync();

        return Ok(ConvertirADto(usuario));
    }

    // POST /api/admin/usuarios/{id}/activar
    [HttpPost("{id:int}/activar")]
    public async Task<ActionResult<UsuarioAdminDto>> Activar(int id)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        usuario.Activo = true;
        await _db.SaveChangesAsync();
        return Ok(ConvertirADto(usuario));
    }

    // POST /api/admin/usuarios/{id}/restablecer-contrasena
    // Genera una nueva contraseña temporal (por ejemplo, si la olvidó). Se devuelve una sola vez.
    [HttpPost("{id:int}/restablecer-contrasena")]
    public async Task<ActionResult<ContrasenaTemporalDto>> RestablecerContrasena(int id)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        if (id == IdActual)
        {
            return BadRequest(new { mensaje = "Para su propia cuenta, use \"Cambiar contraseña\"." });
        }

        var temporal = ContrasenasTemporales.Generar();
        usuario.ContrasenaHash = _cifrador.HashPassword(usuario, temporal);
        usuario.DebeCambiarContrasena = true;
        // La contraseña anterior ya no sirve, y sus sesiones abiertas se cierran.
        usuario.RenovarSello();
        await _db.SaveChangesAsync();

        return Ok(new ContrasenaTemporalDto { Usuario = ConvertirADto(usuario), ContrasenaTemporal = temporal });
    }

    // PUT /api/admin/usuarios/{id}/avisos
    // Correo y qué avisos recibe (el aviso inmediato y/o el escalamiento).
    [HttpPut("{id:int}/avisos")]
    public async Task<ActionResult<UsuarioAdminDto>> ConfigurarAvisos(int id, AvisosUsuarioRequest solicitud)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        usuario.Correo = string.IsNullOrWhiteSpace(solicitud.Correo) ? null : solicitud.Correo.Trim();
        usuario.RecibeAlertas = solicitud.RecibeAlertas;
        usuario.EsContactoEscalamiento = solicitud.EsContactoEscalamiento;
        await _db.SaveChangesAsync();

        return Ok(ConvertirADto(usuario));
    }

    // Comprueba que exista otro administrador activo además del indicado,
    // para que nadie deje el sistema sin acceso de administración.
    private Task<bool> HayOtroAdministradorAsync(int excepto) =>
        _db.Usuarios.AnyAsync(u => u.Id != excepto && u.Activo && u.Rol == RolUsuario.Administrador);

    private static UsuarioAdminDto ConvertirADto(Usuario u) => new()
    {
        Id = u.Id,
        NombreCompleto = u.NombreCompleto,
        NombreUsuario = u.NombreUsuario,
        Rol = u.Rol.ToString(),
        Activo = u.Activo,
        DebeCambiarContrasena = u.DebeCambiarContrasena,
        FechaRegistro = u.FechaRegistro,
        Correo = u.Correo,
        RecibeAlertas = u.RecibeAlertas,
        EsContactoEscalamiento = u.EsContactoEscalamiento,
        TelegramVinculado = u.TelegramChatId != null
    };
}
