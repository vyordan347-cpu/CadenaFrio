using CadenaFrio.Domain.Entities;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

// Lectura ya validada, lista para registrar.
public record LecturaEntrante(double Temperatura, DateTime FechaMedicion);

// Resultado del registro: lecturas guardadas y cuántas ya existían.
public record ResultadoRegistro(IReadOnlyList<Lectura> Guardadas, int Duplicadas);

// Guarda lecturas (una o un lote) sin duplicados y luego las pasa al motor de alertas.
// Lo usan los dos endpoints de lecturas, así la regla está escrita una sola vez.
public class RegistroLecturas
{
    private readonly CadenaFrioDbContext _db;
    private readonly MotorAlertas _motorAlertas;

    public RegistroLecturas(CadenaFrioDbContext db, MotorAlertas motorAlertas)
    {
        _db = db;
        _motorAlertas = motorAlertas;
    }

    public async Task<ResultadoRegistro> RegistrarAsync(int equipoId, IReadOnlyList<LecturaEntrante> entrantes, DateTime ahora)
    {
        if (entrantes.Count == 0)
        {
            return new ResultadoRegistro(Array.Empty<Lectura>(), 0);
        }

        // La medición más reciente que existía antes de este registro: permite saber
        // qué lecturas continúan la línea de tiempo y cuáles pertenecen al pasado.
        var ultimaMedicionPrevia = await _db.Lecturas
            .Where(l => l.EquipoId == equipoId)
            .OrderByDescending(l => l.FechaMedicion)
            .Select(l => (DateTime?)l.FechaMedicion)
            .FirstOrDefaultAsync();

        // Mediciones ya registradas en el periodo del lote (por si se reenvió).
        var desde = entrantes.Min(e => e.FechaMedicion);
        var hasta = entrantes.Max(e => e.FechaMedicion);
        var yaRegistradas = (await _db.Lecturas
                .Where(l => l.EquipoId == equipoId && l.FechaMedicion >= desde && l.FechaMedicion <= hasta)
                .Select(l => l.FechaMedicion)
                .ToListAsync())
            .ToHashSet();

        var nuevas = new List<Lectura>();
        var duplicadas = 0;

        foreach (var entrante in entrantes)
        {
            // HashSet.Add devuelve false si ya existía: detecta duplicados tanto contra
            // la base como dentro del mismo lote.
            if (!yaRegistradas.Add(entrante.FechaMedicion))
            {
                duplicadas++;
                continue;
            }

            nuevas.Add(new Lectura
            {
                EquipoId = equipoId,
                Temperatura = entrante.Temperatura,
                FechaMedicion = entrante.FechaMedicion,
                FechaRecepcion = ahora
            });
        }

        _db.Lecturas.AddRange(nuevas);
        await _db.SaveChangesAsync();

        await _motorAlertas.EvaluarAsync(equipoId, nuevas, ultimaMedicionPrevia);

        return new ResultadoRegistro(nuevas, duplicadas);
    }
}
