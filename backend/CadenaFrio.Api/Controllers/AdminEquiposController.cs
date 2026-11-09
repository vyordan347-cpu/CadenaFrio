using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Gestión de equipos desde el panel de administrador (RF19).
// Toda la clase exige el rol Administrador: los demás reciben 403.
[ApiController]
[Authorize(Policy = Politicas.EsAdministrador)]
[Route("api/admin/equipos")]
public class AdminEquiposController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;
    private readonly Bitacora _bitacora;

    public AdminEquiposController(CadenaFrioDbContext db, Bitacora bitacora)
    {
        _db = db;
        _bitacora = bitacora;
    }

    // Anota la acción sobre un equipo en la bitácora. Nunca incluye la clave del dispositivo.
    private void Anotar(string accion, Equipo equipo, string? extra = null) =>
        _bitacora.Registrar(CategoriaAuditoria.Administracion, accion,
            User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!,
            equipo.Nombre + (extra is null ? "" : $": {extra}"));

    // GET /api/admin/equipos/direcciones
    // Direcciones con las que un celular de la misma red llega a este servidor, por ejemplo
    // "http://192.168.1.20:5067". Las usa la etiqueta QR de cada equipo: "localhost" solo
    // funciona en la propia PC. Es una consulta: no se registra en la bitácora.
    [HttpGet("direcciones")]
    public ActionResult<List<string>> Direcciones()
    {
        var puerto = HttpContext.Connection.LocalPort > 0 ? HttpContext.Connection.LocalPort : Request.Host.Port ?? 80;
        var direcciones = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(u => u.Address)
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && !EsAutoasignada(ip))
            .Distinct()
            // Primero las de una red local (el wifi de la posta); al final las de VPN u otras.
            .OrderBy(PrioridadRedLocal)
            .Select(ip => $"{Request.Scheme}://{ip}:{puerto}")
            .ToList();
        return Ok(direcciones);
    }

    // 169.254.x.x: dirección que Windows se pone sola cuando no hay red. No sirve para el celular.
    private static bool EsAutoasignada(IPAddress ip) => ip.GetAddressBytes() is [169, 254, ..];

    // 0 = 192.168.x.x (lo más común en un wifi), 1 = 10.x.x.x, 2 = 172.16–31.x.x, 3 = otras.
    private static int PrioridadRedLocal(IPAddress ip) => ip.GetAddressBytes() switch
    {
        [192, 168, ..] => 0,
        [10, ..] => 1,
        [172, >= 16 and <= 31, ..] => 2,
        _ => 3
    };

    // GET /api/admin/equipos
    [HttpGet]
    public async Task<ActionResult<List<EquipoAdminDto>>> Listar()
    {
        var equipos = await _db.Equipos
            .AsNoTracking()
            .OrderByDescending(e => e.Activo)
            .ThenBy(e => e.Nombre)
            .Select(e => new EquipoAdminDto
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Tipo = (TipoEquipoDto)(int)e.Tipo,
                Ubicacion = e.Ubicacion,
                Activo = e.Activo,
                FechaRegistro = e.FechaRegistro,
                // Subconsulta: la recepción más reciente de cada equipo.
                UltimaSenal = e.Lecturas
                    .OrderByDescending(l => l.FechaRecepcion)
                    .Select(l => (DateTime?)l.FechaRecepcion)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(equipos);
    }

    // POST /api/admin/equipos
    // Registra un equipo y genera su clave. La clave se devuelve una sola vez.
    [HttpPost]
    public async Task<ActionResult<ClaveGeneradaDto>> Registrar(GuardarEquipoRequest solicitud)
    {
        var clave = ClavesDispositivo.Generar();

        var equipo = new Equipo
        {
            Nombre = solicitud.Nombre!.Trim(),
            Tipo = (TipoEquipo)(int)solicitud.Tipo!.Value,
            Ubicacion = solicitud.Ubicacion?.Trim() ?? string.Empty,
            ClaveDispositivo = ClavesDispositivo.Hash(clave),
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };

        _db.Equipos.Add(equipo);
        Anotar("Registró un equipo", equipo, $"{equipo.Tipo}, {equipo.Ubicacion}");
        await _db.SaveChangesAsync();

        return Ok(new ClaveGeneradaDto { Equipo = ConvertirADto(equipo, ultimaSenal: null), Clave = clave });
    }

    // PUT /api/admin/equipos/{id}
    // Edita nombre, tipo y ubicación. La clave no cambia.
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EquipoAdminDto>> Editar(int id, GuardarEquipoRequest solicitud)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        var antes = $"{equipo.Nombre}, {equipo.Tipo}, {equipo.Ubicacion}";
        equipo.Nombre = solicitud.Nombre!.Trim();
        equipo.Tipo = (TipoEquipo)(int)solicitud.Tipo!.Value;
        equipo.Ubicacion = solicitud.Ubicacion?.Trim() ?? string.Empty;
        Anotar("Editó un equipo", equipo, $"antes: {antes}; ahora: {equipo.Tipo}, {equipo.Ubicacion}");
        await _db.SaveChangesAsync();

        return Ok(ConvertirADto(equipo, await UltimaSenalAsync(id)));
    }

    // POST /api/admin/equipos/{id}/regenerar-clave
    // Reemplaza la clave: la anterior deja de funcionar al instante.
    [HttpPost("{id:int}/regenerar-clave")]
    public async Task<ActionResult<ClaveGeneradaDto>> RegenerarClave(int id)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        var clave = ClavesDispositivo.Generar();
        equipo.ClaveDispositivo = ClavesDispositivo.Hash(clave);
        Anotar("Regeneró la clave de un equipo", equipo, "la clave anterior dejó de funcionar");
        await _db.SaveChangesAsync();

        return Ok(new ClaveGeneradaDto { Equipo = ConvertirADto(equipo, await UltimaSenalAsync(id)), Clave = clave });
    }

    // POST /api/admin/equipos/{id}/desactivar
    // El equipo deja de aceptar lecturas y el vigilante lo ignora. Su historial se conserva.
    [HttpPost("{id:int}/desactivar")]
    public async Task<ActionResult<EquipoAdminDto>> Desactivar(int id)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        equipo.Activo = false;

        // Sus alertas abiertas se cierran: un equipo fuera de servicio no debe seguir
        // mostrando alertas "en curso" para siempre. Las que exigen revisión siguen pendientes.
        var ahora = DateTime.UtcNow;
        var abiertas = await _db.Alertas.Where(a => a.EquipoId == id && a.Fin == null).ToListAsync();
        foreach (var alerta in abiertas)
        {
            alerta.Fin = ahora;
        }

        Anotar("Desactivó un equipo", equipo, abiertas.Count == 0 ? null : $"se cerraron {abiertas.Count} alertas abiertas");
        await _db.SaveChangesAsync();
        return Ok(ConvertirADto(equipo, await UltimaSenalAsync(id)));
    }

    // POST /api/admin/equipos/{id}/activar
    [HttpPost("{id:int}/activar")]
    public async Task<ActionResult<EquipoAdminDto>> Activar(int id)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.Id == id);
        if (equipo is null)
        {
            return NotFound(new { mensaje = "Equipo no encontrado." });
        }

        equipo.Activo = true;
        Anotar("Activó un equipo", equipo);
        await _db.SaveChangesAsync();
        return Ok(ConvertirADto(equipo, await UltimaSenalAsync(id)));
    }

    private Task<DateTime?> UltimaSenalAsync(int equipoId) =>
        _db.Lecturas
            .Where(l => l.EquipoId == equipoId)
            .OrderByDescending(l => l.FechaRecepcion)
            .Select(l => (DateTime?)l.FechaRecepcion)
            .FirstOrDefaultAsync();

    private static EquipoAdminDto ConvertirADto(Equipo e, DateTime? ultimaSenal) => new()
    {
        Id = e.Id,
        Nombre = e.Nombre,
        Tipo = (TipoEquipoDto)(int)e.Tipo,
        Ubicacion = e.Ubicacion,
        Activo = e.Activo,
        FechaRegistro = e.FechaRegistro,
        UltimaSenal = ultimaSenal
    };
}
