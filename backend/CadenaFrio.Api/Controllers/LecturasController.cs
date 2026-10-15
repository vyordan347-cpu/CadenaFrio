using CadenaFrio.Domain.Entities;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Controllers;

// Puerta de entrada de las lecturas enviadas por los dispositivos (HU01, RF01).
[ApiController]
[Route("api/lecturas")]
public class LecturasController : ControllerBase
{
    private const string EncabezadoClave = "X-Clave-Dispositivo";

    // Margen para pequeñas diferencias entre el reloj del dispositivo y el del servidor.
    private static readonly TimeSpan ToleranciaFuturo = TimeSpan.FromMinutes(5);

    private readonly CadenaFrioDbContext _db;
    private readonly MotorAlertas _motorAlertas;

    // El DbContext y el motor de alertas llegan por inyección de dependencias (Program.cs).
    public LecturasController(CadenaFrioDbContext db, MotorAlertas motorAlertas)
    {
        _db = db;
        _motorAlertas = motorAlertas;
    }

    // POST /api/lecturas
    [HttpPost]
    public async Task<IActionResult> Registrar(
        [FromHeader(Name = EncabezadoClave)] string? clave,
        [FromBody] RegistrarLecturaRequest solicitud)
    {
        // 1. Identificar el equipo por su clave (RNF06).
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Unauthorized(new { mensaje = "Falta la clave del dispositivo." });
        }

        var equipo = await _db.Equipos
            .FirstOrDefaultAsync(e => e.ClaveDispositivo == clave && e.Activo);

        if (equipo is null)
        {
            return Unauthorized(new { mensaje = "Clave de dispositivo no válida o equipo inactivo." });
        }

        // 2. Validar la fecha. La temperatura ya fue validada por [Range] en el DTO.
        var ahora = DateTime.UtcNow;
        var fechaMedicion = ConvertirAUtc(solicitud.FechaMedicion ?? ahora);

        if (fechaMedicion > ahora + ToleranciaFuturo)
        {
            return BadRequest(new { mensaje = "La fecha de medición está en el futuro. Revise el reloj del dispositivo." });
        }

        // 3. Guardar la lectura.
        var lectura = new Lectura
        {
            EquipoId = equipo.Id,
            Temperatura = solicitud.Temperatura!.Value,
            FechaMedicion = fechaMedicion,
            FechaRecepcion = ahora
        };

        _db.Lecturas.Add(lectura);
        await _db.SaveChangesAsync();

        // 4. Evaluar si la lectura abre, actualiza o cierra una alerta (HU03).
        await _motorAlertas.EvaluarAsync(lectura);

        return Ok(new
        {
            lecturaId = lectura.Id,
            equipo = equipo.Nombre,
            fechaRecepcion = ahora
        });
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
