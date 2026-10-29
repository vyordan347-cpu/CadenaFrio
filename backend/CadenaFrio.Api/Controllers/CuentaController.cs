using System.Security.Claims;
using System.Security.Cryptography;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Api.Controllers;

// Avisos del propio usuario: vincular o desvincular Telegram.
[ApiController]
[Authorize]
[Route("api/cuenta/avisos")]
public class CuentaController : ControllerBase
{
    private readonly CadenaFrioDbContext _db;
    private readonly OpcionesTelegram _telegram;

    public CuentaController(CadenaFrioDbContext db, IOptions<OpcionesNotificaciones> opciones)
    {
        _db = db;
        _telegram = opciones.Value.Telegram;
    }

    private int IdActual => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/cuenta/avisos
    [HttpGet]
    public async Task<ActionResult<MisAvisosDto>> Obtener()
    {
        var u = await _db.Usuarios.AsNoTracking().FirstAsync(x => x.Id == IdActual);
        return Ok(new MisAvisosDto
        {
            TelegramDisponible = _telegram.Configurado,
            TelegramVinculado = u.TelegramChatId is not null,
            Correo = u.Correo,
            RecibeAlertas = u.RecibeAlertas,
            EsContactoEscalamiento = u.EsContactoEscalamiento
        });
    }

    // POST /api/cuenta/avisos/telegram
    // Genera un código de un solo uso y el enlace que abre el bot con ese código.
    [HttpPost("telegram")]
    public async Task<ActionResult<EnlaceTelegramDto>> GenerarEnlace()
    {
        if (!_telegram.Configurado)
        {
            return BadRequest(new { mensaje = "El bot de Telegram aún no está configurado en el servidor." });
        }

        var usuario = await _db.Usuarios.FirstAsync(x => x.Id == IdActual);
        usuario.CodigoVinculacionTelegram = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        await _db.SaveChangesAsync();

        return Ok(new EnlaceTelegramDto { Enlace = $"https://t.me/{_telegram.NombreBot}?start={usuario.CodigoVinculacionTelegram}" });
    }

    // DELETE /api/cuenta/avisos/telegram
    [HttpDelete("telegram")]
    public async Task<IActionResult> Desvincular()
    {
        var usuario = await _db.Usuarios.FirstAsync(x => x.Id == IdActual);
        usuario.TelegramChatId = null;
        usuario.CodigoVinculacionTelegram = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
