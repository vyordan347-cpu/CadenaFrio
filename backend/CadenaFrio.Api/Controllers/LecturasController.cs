using CadenaFrio.Domain.Entities;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Puerta de entrada de las lecturas enviadas por los dispositivos (HU01, HU06, RF01, RF13).
// No usa sesión de usuario: cada dispositivo se identifica con su clave (RNF06).
[ApiController]
[Route("api/lecturas")]
public class LecturasController : ControllerBase
{
    private const string EncabezadoClave = "X-Clave-Dispositivo";

    // Margen para pequeñas diferencias entre el reloj del dispositivo y el del servidor.
    private static readonly TimeSpan ToleranciaFuturo = TimeSpan.FromMinutes(5);

    private readonly CadenaFrioDbContext _db;
    private readonly RegistroLecturas _registro;

    public LecturasController(CadenaFrioDbContext db, RegistroLecturas registro)
    {
        _db = db;
        _registro = registro;
    }

    // POST /api/lecturas
    // Una lectura en vivo.
    [HttpPost]
    public async Task<IActionResult> Registrar(
        [FromHeader(Name = EncabezadoClave)] string? clave,
        [FromBody] RegistrarLecturaRequest solicitud)
    {
        var equipo = await IdentificarEquipoAsync(clave);
        if (equipo is null)
        {
            return Unauthorized(new { mensaje = "Clave de dispositivo no válida o equipo inactivo." });
        }

        // La temperatura ya fue validada por [Range] en el DTO.
        var ahora = DateTime.UtcNow;
        var fechaMedicion = ConvertirAUtc(solicitud.FechaMedicion ?? ahora);

        if (fechaMedicion > ahora + ToleranciaFuturo)
        {
            return BadRequest(new { mensaje = "La fecha de medición está en el futuro. Revise el reloj del dispositivo." });
        }

        var resultado = await _registro.RegistrarAsync(
            equipo.Id, new[] { new LecturaEntrante(solicitud.Temperatura!.Value, fechaMedicion) }, ahora);

        return Ok(new
        {
            lecturaId = resultado.Guardadas.FirstOrDefault()?.Id,
            // Si el dispositivo reenvía la misma lectura, no se duplica.
            duplicada = resultado.Duplicadas > 0,
            equipo = equipo.Nombre,
            fechaRecepcion = ahora
        });
    }

    // POST /api/lecturas/lote  (HU06, RF13)
    // Lecturas guardadas en el dispositivo mientras no tenía conexión, enviadas juntas.
    // Cada lectura se valida por separado: una inválida se descarta sin rechazar el lote.
    [HttpPost("lote")]
    public async Task<ActionResult<ResultadoLoteDto>> RegistrarLote(
        [FromHeader(Name = EncabezadoClave)] string? clave,
        [FromBody] RegistrarLoteRequest solicitud)
    {
        var equipo = await IdentificarEquipoAsync(clave);
        if (equipo is null)
        {
            return Unauthorized(new { mensaje = "Clave de dispositivo no válida o equipo inactivo." });
        }

        var ahora = DateTime.UtcNow;
        var resultado = new ResultadoLoteDto { Recibidas = solicitud.Lecturas!.Count };
        var validas = new List<LecturaEntrante>();

        for (var i = 0; i < solicitud.Lecturas.Count; i++)
        {
            var lectura = solicitud.Lecturas[i];
            var posicion = i + 1;

            if (lectura.Temperatura is not { } temperatura || !LimitesFisicos.EsValida(temperatura))
            {
                resultado.Descartadas.Add($"Lectura {posicion}: temperatura ausente o fuera de -50 °C a 60 °C.");
                continue;
            }

            if (lectura.FechaMedicion is null)
            {
                resultado.Descartadas.Add($"Lectura {posicion}: falta la fecha de medición.");
                continue;
            }

            var fechaMedicion = ConvertirAUtc(lectura.FechaMedicion.Value);
            if (fechaMedicion > ahora + ToleranciaFuturo)
            {
                resultado.Descartadas.Add($"Lectura {posicion}: la fecha de medición está en el futuro.");
                continue;
            }

            validas.Add(new LecturaEntrante(temperatura, fechaMedicion));
        }

        var registro = await _registro.RegistrarAsync(equipo.Id, validas, ahora);
        resultado.Guardadas = registro.Guardadas.Count;
        resultado.Duplicadas = registro.Duplicadas;

        return Ok(resultado);
    }

    // Identifica el equipo por su clave (RNF06). Nulo si falta, no existe o está inactivo.
    private async Task<Equipo?> IdentificarEquipoAsync(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return null;
        }

        return await _db.Equipos.FirstOrDefaultAsync(e => e.ClaveDispositivo == clave && e.Activo);
    }

    // Si la fecha llega sin zona horaria, se asume UTC, porque el ESP32
    // obtiene la hora en UTC desde internet.
    private static DateTime ConvertirAUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha,
        DateTimeKind.Local => fecha.ToUniversalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
    };
}
