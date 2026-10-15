using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Servicios;

// Detecta los equipos que dejaron de enviar datos (HU04, HU05).
// El motor de alertas solo se activa cuando llega una lectura; si no llega nada,
// nunca se ejecuta. Por eso hace falta este revisor, que el vigilante llama periódicamente.
public class DetectorSilencio
{
    private readonly CadenaFrioDbContext _db;
    private readonly INotificadorAlertas _notificador;
    private readonly OpcionesMonitoreo _opciones;

    public DetectorSilencio(CadenaFrioDbContext db, INotificadorAlertas notificador, IOptions<OpcionesMonitoreo> opciones)
    {
        _db = db;
        _notificador = notificador;
        _opciones = opciones.Value;
    }

    public async Task RevisarAsync(CancellationToken cancelacion)
    {
        var ahora = DateTime.UtcNow;

        var equipos = await _db.Equipos
            .AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new { e.Id, e.Nombre })
            .ToListAsync(cancelacion);

        foreach (var equipo in equipos)
        {
            // Último dato recibido, según cuándo llegó al servidor (no cuándo se midió):
            // lo que importa es cuándo tuvimos noticias del dispositivo por última vez.
            var ultima = await _db.Lecturas
                .AsNoTracking()
                .Where(l => l.EquipoId == equipo.Id)
                .OrderByDescending(l => l.FechaRecepcion)
                .Select(l => new { l.FechaRecepcion, l.Temperatura })
                .FirstOrDefaultAsync(cancelacion);

            // Un equipo que nunca envió datos no "perdió" la señal: su estado es "Sin datos".
            if (ultima is null)
            {
                continue;
            }

            var silencio = ahora - ultima.FechaRecepcion;

            var alerta = await _db.Alertas.FirstOrDefaultAsync(a =>
                a.EquipoId == equipo.Id && a.Tipo == TipoAlerta.SinSenal && a.Fin == null, cancelacion);

            if (alerta is null && silencio > _opciones.LimiteSinSenal)
            {
                // 1. Abrir: el silencio empezó con el último dato recibido, no cuando se detectó.
                var limites = await _db.ObtenerLimitesAsync(equipo.Id);

                alerta = new Alerta
                {
                    EquipoId = equipo.Id,
                    Tipo = TipoAlerta.SinSenal,
                    // No sabemos qué temperatura hay, solo que perdimos la vista: es una advertencia.
                    Nivel = NivelAlerta.Advertencia,
                    Inicio = ultima.FechaRecepcion,
                    // Última temperatura conocida antes del silencio.
                    ValorExtremo = ultima.Temperatura,
                    OptimoMinimo = limites.OptimoMinimo,
                    OptimoMaximo = limites.OptimoMaximo,
                    LimiteMinimo = limites.LimiteMinimo,
                    LimiteMaximo = limites.LimiteMaximo
                };

                _db.Alertas.Add(alerta);
                await _db.SaveChangesAsync(cancelacion);
                await _notificador.AlertaAbiertaAsync(alerta, equipo.Nombre);
            }

            if (alerta is not null
                && alerta.RevisionRequeridaDesde is null
                && ahora - alerta.Inicio >= _opciones.LimiteSinSenalParaRevision)
            {
                // 2. Exigir revisión: el silencio es tan largo que la refrigeradora pudo
                //    calentarse sin que nadie lo sepa. Alguien debe verificar las vacunas.
                alerta.RevisionRequeridaDesde = alerta.Inicio + _opciones.LimiteSinSenalParaRevision;
                await _db.SaveChangesAsync(cancelacion);
                await _notificador.AlertaEscaladaAsync(alerta, equipo.Nombre);
            }
        }
    }
}
