using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Tests;

// Base de datos SQLite en memoria: se crea en cada prueba y desaparece al terminar.
// Es la misma base real (mismas tablas, restricciones e índices), sin tocar cadenafrio.db.
public sealed class BaseDeDatosDePrueba : IDisposable
{
    private readonly SqliteConnection _conexion;

    public CadenaFrioDbContext Db { get; }

    // El equipo 1 ("Refrigeradora de prueba") ya existe: lo crean los datos semilla.
    public const int EquipoId = 1;

    public BaseDeDatosDePrueba()
    {
        // La base en memoria vive mientras la conexión esté abierta.
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        var opciones = new DbContextOptionsBuilder<CadenaFrioDbContext>()
            .UseSqlite(_conexion)
            .Options;

        Db = new CadenaFrioDbContext(opciones);
        Db.Database.EnsureCreated();
    }

    // Opciones de monitoreo para las pruebas (detección de sensor desactivada, como en el prototipo).
    public static IOptions<OpcionesMonitoreo> Monitoreo(int minutosSinSenal = 5, int minutosParaRevision = 30) =>
        Options.Create(new OpcionesMonitoreo
        {
            MinutosSinSenal = minutosSinSenal,
            MinutosSinSenalParaRevision = minutosParaRevision,
            MinutosSensorSinVariacion = 0
        });

    public void Dispose()
    {
        Db.Dispose();
        _conexion.Dispose();
    }
}

// Notificador falso: en lugar de enviar avisos, los anota para que la prueba los revise.
public sealed class NotificadorDePrueba : INotificadorAlertas
{
    public List<string> Eventos { get; } = new();

    public Task AlertaAbiertaAsync(Alerta alerta, string nombreEquipo)
    {
        Eventos.Add($"Abierta:{alerta.Tipo}:{alerta.Nivel}");
        return Task.CompletedTask;
    }

    public Task AlertaEscaladaAsync(Alerta alerta, string nombreEquipo)
    {
        Eventos.Add($"Escalada:{alerta.Tipo}");
        return Task.CompletedTask;
    }

    public Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo)
    {
        Eventos.Add($"Cerrada:{alerta.Tipo}");
        return Task.CompletedTask;
    }
}
