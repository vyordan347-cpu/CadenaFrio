using System.Security.Claims;
using CadenaFrio.Api.Reportes;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Api.Controllers;

// Revisión de alertas por el personal (RF08).
// Cualquier usuario con sesión puede decir "Lo estoy atendiendo" y revisar silencios y
// advertencias: el técnico suele ser quien está presente. La revisión de una ruptura (Anexo 13)
// la registran el responsable de inmunizaciones, el médico jefe o el administrador (6.10.2).
[ApiController]
[Authorize]
[Route("api/alertas")]
public class AlertasController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;
    private readonly ReconocimientoAlertas _reconocimiento;
    private readonly Bitacora _bitacora;
    private readonly IAuthorizationService _autorizacion;

    public AlertasController(CadenaFrioDbContext db, ReconocimientoAlertas reconocimiento, Bitacora bitacora,
        IAuthorizationService autorizacion)
    {
        _db = db;
        _reconocimiento = reconocimiento;
        _bitacora = bitacora;
        _autorizacion = autorizacion;
    }

    // POST /api/alertas/{id}/reconocer
    // "Lo estoy atendiendo" desde la web: detiene el escalamiento y avisa a los demás.
    [HttpPost("{id:int}/reconocer")]
    public async Task<IActionResult> Reconocer(int id)
    {
        var usuario = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;
        var resultado = await _reconocimiento.ReconocerAsync(id, usuario);
        return resultado.Exito ? NoContent() : Conflict(new { mensaje = resultado.Mensaje });
    }

    // GET /api/alertas/catalogo-anexo13
    // Listas oficiales del Anexo N° 13 para el formulario de revisión de rupturas.
    [HttpGet("catalogo-anexo13")]
    public ActionResult<CatalogoAnexo13Dto> ObtenerCatalogoAnexo13()
    {
        static List<OpcionAnexo13Dto> Convertir(IEnumerable<OpcionAnexo13> opciones) =>
            opciones.Select(o => new OpcionAnexo13Dto
            {
                Codigo = o.Codigo,
                Descripcion = o.Descripcion,
                Grupo = o.Grupo,
                RequiereDetalle = o.RequiereDetalle
            }).ToList();

        return new CatalogoAnexo13Dto
        {
            CausasRefrigerador = Convertir(CatalogoAnexo13.CausasRefrigerador),
            CausasTermo = Convertir(CatalogoAnexo13.CausasTermo),
            FormasRestablecimiento = Convertir(CatalogoAnexo13.FormasRestablecimiento)
        };
    }

    // GET /api/alertas/pendientes?equipoId=1   (sin equipoId: las de todos los equipos)
    // Alertas que exigen revisión y no la tienen: nunca atendidas, o atendidas antes de
    // que la exigieran. Incluye rupturas y silencios prolongados, y también las que ya
    // terminaron: que la situación se normalice no significa que alguien lo vio.
    [HttpGet("pendientes")]
    public async Task<ActionResult<List<AlertaPendienteDto>>> ObtenerPendientes([FromQuery] int? equipoId)
    {
        var pendientes = await _db.Alertas
            .AsNoTracking()
            .Where(a => (equipoId == null || a.EquipoId == equipoId)
                        && a.RevisionRequeridaDesde != null
                        && (a.FechaAtencion == null || a.FechaAtencion < a.RevisionRequeridaDesde))
            .OrderBy(a => a.Inicio)
            .Select(a => new AlertaPendienteDto
            {
                Id = a.Id,
                EquipoId = a.EquipoId,
                NombreEquipo = a.Equipo.Nombre,
                TipoEquipo = (TipoEquipoDto)(int)a.Equipo.Tipo,
                Tipo = (TipoAlertaDto)(int)a.Tipo,
                Inicio = a.Inicio,
                FechaRuptura = a.FechaRuptura,
                Fin = a.Fin,
                DetectadaTarde = a.DetectadaTarde,
                ValorExtremo = a.ValorExtremo,
                LimiteMinimo = a.LimiteMinimo,
                LimiteMaximo = a.LimiteMaximo,
                // EF Core calcula esta en memoria, después de leer la fila.
                RequiereAnexo13 = CatalogoAnexo13.Aplica(a.Tipo, a.Nivel)
            })
            .ToListAsync();

        // El plazo de 24 h corre desde que se detectó la ruptura, no desde la hora de la lectura:
        // si el equipo estuvo sin conexión, la ruptura se conoce al reconectarse.
        foreach (var p in pendientes.Where(p => p.RequiereAnexo13))
        {
            p.FechaDeteccion = await _db.ObtenerDeteccionRupturaAsync(p.EquipoId, p.FechaRuptura);
            p.FechaLimiteNotificacion = p.FechaDeteccion + CatalogoAnexo13.PlazoNotificacion;
        }

        return Ok(pendientes);
    }

    // GET /api/alertas/{id}/anexo13
    // Excel del formato de notificación de ruptura (Anexo N° 13), prellenado con lo que
    // el sistema sabe. Solo para rupturas de temperatura ya revisadas con ese formato.
    [HttpGet("{id:int}/anexo13")]
    public async Task<IActionResult> DescargarAnexo13(int id, [FromServices] IOptions<OpcionesEstablecimiento> establecimiento)
    {
        var alerta = await _db.Alertas
            .AsNoTracking()
            .Include(a => a.Equipo).ThenInclude(e => e.Vacunas).ThenInclude(ev => ev.Vacuna)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alerta is null)
        {
            return NotFound(new { mensaje = "Alerta no encontrada." });
        }

        if (!CatalogoAnexo13.Aplica(alerta.Tipo, alerta.Nivel) || alerta.CausaRuptura is null)
        {
            return Conflict(new { mensaje = "El Anexo 13 se genera para rupturas revisadas con ese formato." });
        }

        var lecturas = _db.Lecturas.AsNoTracking().Where(l => l.EquipoId == alerta.EquipoId);

        // Último registro dentro del rango límite antes de la ruptura.
        var ultimoNormal = await lecturas
            .Where(l => l.FechaMedicion < alerta.FechaRuptura
                        && l.Temperatura >= alerta.LimiteMinimo && l.Temperatura <= alerta.LimiteMaximo)
            .OrderByDescending(l => l.FechaMedicion)
            .Select(l => new { l.FechaMedicion, l.Temperatura })
            .FirstOrDefaultAsync();

        // Temperaturas exactas al detectar la ruptura y al volver al rango óptimo.
        async Task<double?> TemperaturaEnAsync(DateTime? momento) => momento is null
            ? null
            : await lecturas.Where(l => l.FechaMedicion == momento).Select(l => (double?)l.Temperatura).FirstOrDefaultAsync();

        // AtendidaPor guarda el nombre completo de quien revisó.
        var correo = await _db.Usuarios
            .Where(u => u.NombreCompleto == alerta.AtendidaPor)
            .Select(u => u.Correo)
            .FirstOrDefaultAsync();

        var datos = new DatosAnexo13(
            alerta,
            await _db.ObtenerDeteccionRupturaAsync(alerta.EquipoId, alerta.FechaRuptura),
            alerta.Equipo.Vacunas.Select(ev => ev.Vacuna.Nombre).OrderBy(n => n).ToList(),
            await TemperaturaEnAsync(alerta.FechaRuptura),
            ultimoNormal?.FechaMedicion,
            ultimoNormal?.Temperatura,
            await TemperaturaEnAsync(alerta.Fin),
            correo,
            establecimiento.Value);

        var nombre = $"anexo13-ruptura-{alerta.Id}-{alerta.FechaRuptura!.Value.AddHours(-5):yyyy-MM-dd}.xlsx";
        return File(FormatoAnexo13.Generar(datos),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    // POST /api/alertas/{id}/atender
    // Registra quién revisó la alerta, cuándo y qué hizo.
    [HttpPost("{id:int}/atender")]
    public async Task<IActionResult> Atender(int id, AtenderAlertaRequest solicitud)
    {
        var alerta = await _db.Alertas.Include(a => a.Equipo).FirstOrDefaultAsync(a => a.Id == id);

        if (alerta is null)
        {
            return NotFound(new { mensaje = "Alerta no encontrada." });
        }

        // Una alerta atendida después de que exigiera revisión no necesita otra.
        var yaAtendida = alerta.FechaAtencion is not null
                         && (alerta.RevisionRequeridaDesde is null || alerta.FechaAtencion >= alerta.RevisionRequeridaDesde);
        if (yaAtendida)
        {
            return Conflict(new { mensaje = $"Esta alerta ya fue atendida por {alerta.AtendidaPor}." });
        }

        var observacion = string.IsNullOrWhiteSpace(solicitud.Observacion) ? null : solicitud.Observacion.Trim();

        if (CatalogoAnexo13.Aplica(alerta.Tipo, alerta.Nivel))
        {
            // La notificación de la ruptura la hace el jefe del establecimiento a través del
            // responsable de inmunizaciones (NTS 242, 6.10.2): los demás roles reciben 403.
            if (!(await _autorizacion.AuthorizeAsync(User, Politicas.PuedeRevisarRupturas)).Succeeded)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    mensaje = "La revisión de una ruptura la registra el responsable de inmunizaciones o el médico jefe del establecimiento."
                });
            }

            // Ruptura de temperatura: notificación con las listas del Anexo N° 13.
            var error = CatalogoAnexo13.Validar(alerta.Equipo.Tipo,
                solicitud.CausaRuptura, solicitud.DetalleCausa,
                solicitud.FormaRestablecimiento, solicitud.DetalleRestablecimiento,
                solicitud.VacunasInmovilizadas);
            if (error is not null)
            {
                return BadRequest(new { mensaje = error });
            }

            // El detalle solo se guarda si la opción elegida lo pide ("Otro", "Otra causa").
            var causa = CatalogoAnexo13.BuscarCausa(alerta.Equipo.Tipo, solicitud.CausaRuptura)!;
            var forma = CatalogoAnexo13.BuscarFormaRestablecimiento(solicitud.FormaRestablecimiento)!;

            alerta.CausaRuptura = causa.Codigo;
            alerta.DetalleCausa = causa.RequiereDetalle ? solicitud.DetalleCausa!.Trim() : null;
            alerta.FormaRestablecimiento = forma.Codigo;
            alerta.DetalleRestablecimiento = forma.RequiereDetalle ? solicitud.DetalleRestablecimiento!.Trim() : null;
            alerta.VacunasInmovilizadas = solicitud.VacunasInmovilizadas;
        }
        else if (observacion is null || observacion.Length < 10)
        {
            // Silencio prolongado u otra alerta: basta con describir qué se hizo.
            return BadRequest(new { mensaje = "Describa las acciones realizadas (al menos 10 caracteres)." });
        }

        alerta.AtendidaPor = User.FindFirstValue(ClaimTiposCadenaFrio.NombreCompleto) ?? User.Identity!.Name!;
        alerta.FechaAtencion = DateTime.UtcNow;
        alerta.ObservacionAtencion = observacion;

        if (CatalogoAnexo13.Aplica(alerta.Tipo, alerta.Nivel))
        {
            var causa = CatalogoAnexo13.BuscarCausa(alerta.Equipo.Tipo, alerta.CausaRuptura)!.Descripcion;
            var forma = CatalogoAnexo13.BuscarFormaRestablecimiento(alerta.FormaRestablecimiento)!.Descripcion;
            _bitacora.Registrar(CategoriaAuditoria.Alertas, "Revisó una ruptura (Anexo 13)", alerta.AtendidaPor,
                $"{alerta.Equipo.Nombre}, alerta {alerta.Id}: {causa}{Detalle(alerta.DetalleCausa)}. "
                + $"Restableció: {forma}{Detalle(alerta.DetalleRestablecimiento)}. "
                + $"Vacunas inmovilizadas: {(alerta.VacunasInmovilizadas == true ? "Sí" : "No")}.");
        }
        else
        {
            _bitacora.Registrar(CategoriaAuditoria.Alertas, "Revisó una alerta", alerta.AtendidaPor,
                $"{alerta.Equipo.Nombre}, alerta {alerta.Id}: {observacion}");
        }

        await _db.SaveChangesAsync();

        // 204 No Content: se realizó la acción y no hay nada que devolver.
        return NoContent();
    }

    // " (detalle)" cuando la opción elegida fue "Otro" u "Otra causa".
    private static string Detalle(string? texto) => texto is null ? "" : $" ({texto})";
}
