using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CadenaFrio.Api.Controllers;

// Bitácora de auditoría (Fase 5b): solo lectura y solo para el administrador.
// No hay rutas para editar ni borrar registros.
[ApiController]
[Authorize(Policy = Politicas.EsAdministrador)]
[Route("api/admin/bitacora")]
public class AdminBitacoraController : ControllerBase
{
    private readonly ConsultaBitacora _consulta;

    public AdminBitacoraController(ConsultaBitacora consulta)
    {
        _consulta = consulta;
    }

    // GET /api/admin/bitacora?desde=...&hasta=...&usuario=...&categoria=Sesion&pagina=1
    // Fechas en UTC. Todos los filtros son opcionales.
    [HttpGet]
    public async Task<ActionResult<PaginaBitacoraDto>> Consultar(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? usuario, [FromQuery] CategoriaAuditoriaDto? categoria,
        [FromQuery] int pagina = 1)
    {
        if (desde is not null && hasta is not null && desde > hasta)
        {
            return BadRequest(new { mensaje = "La fecha inicial debe ser anterior a la final." });
        }

        var resultado = await _consulta.ConsultarAsync(new FiltroBitacora(
            desde?.ToUniversalTime(), hasta?.ToUniversalTime(), usuario,
            categoria is null ? null : (CategoriaAuditoria)(int)categoria, pagina));

        return new PaginaBitacoraDto
        {
            Pagina = resultado.Pagina,
            TamanoPagina = ConsultaBitacora.TamanoPagina,
            Total = resultado.Total,
            Registros = resultado.Registros.Select(r => new RegistroAuditoriaDto
            {
                Id = r.Id,
                Fecha = r.Fecha,
                Usuario = r.Usuario,
                Categoria = (CategoriaAuditoriaDto)(int)r.Categoria,
                Accion = r.Accion,
                Detalle = r.Detalle
            }).ToList()
        };
    }

    // GET /api/admin/bitacora/usuarios
    // Nombres que aparecen en la bitácora, para el filtro por usuario.
    [HttpGet("usuarios")]
    public async Task<ActionResult<List<string>>> Usuarios() => await _consulta.UsuariosAsync();
}
