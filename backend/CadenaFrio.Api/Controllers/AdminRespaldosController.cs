using System.Security.Claims;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CadenaFrio.Api.Controllers;

// Respaldos de la base de datos (Fase 5c). Solo el administrador.
// No hay ruta para restaurar: se hace a mano con el servidor detenido (docs/restaurar-respaldo.md).
[ApiController]
[Authorize(Policy = Politicas.EsAdministrador)]
[Route("api/admin/respaldos")]
public class AdminRespaldosController : ControllerBase
{
    private readonly RespaldoBaseDatos _respaldos;
    private readonly Bitacora _bitacora;

    public AdminRespaldosController(RespaldoBaseDatos respaldos, Bitacora bitacora)
    {
        _respaldos = respaldos;
        _bitacora = bitacora;
    }

    private string UsuarioActual =>
        User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;

    // GET /api/admin/respaldos
    [HttpGet]
    public ActionResult<List<RespaldoDto>> Listar() =>
        _respaldos.Listar().Select(ConvertirADto).ToList();

    // POST /api/admin/respaldos
    // "Respaldar ahora".
    [HttpPost]
    public async Task<ActionResult<RespaldoDto>> Crear()
    {
        var archivo = _respaldos.Crear(TipoRespaldo.Manual);
        await _bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Administracion,
            "Creó un respaldo de la base", UsuarioActual, archivo.Nombre);

        return ConvertirADto(archivo);
    }

    // GET /api/admin/respaldos/{nombre}
    // Descarga un respaldo. Cada descarga queda en la bitácora: el archivo contiene la base
    // completa (correos del personal, contraseñas cifradas), así que importa saber quién se lo llevó.
    [HttpGet("{nombre}")]
    public async Task<IActionResult> Descargar(string nombre)
    {
        var ruta = _respaldos.RutaDe(nombre);
        if (ruta is null)
        {
            return NotFound(new { mensaje = "Respaldo no encontrado." });
        }

        await _bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Administracion,
            "Descargó un respaldo de la base", UsuarioActual, nombre);

        return PhysicalFile(ruta, "application/octet-stream", nombre);
    }

    private static RespaldoDto ConvertirADto(ArchivoRespaldo a) => new()
    {
        Nombre = a.Nombre,
        Fecha = a.FechaUtc,
        Automatico = a.Tipo == TipoRespaldo.Automatico,
        Bytes = a.Bytes
    };
}
