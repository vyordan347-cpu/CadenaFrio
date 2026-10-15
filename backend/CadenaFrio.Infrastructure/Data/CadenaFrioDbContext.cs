using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Data;

// Conecta las entidades del dominio con la base de datos SQLite.
public class CadenaFrioDbContext : DbContext
{
    public CadenaFrioDbContext(DbContextOptions<CadenaFrioDbContext> options) : base(options)
    {
    }

    // Cada DbSet se convierte en una tabla.
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<Lectura> Lecturas => Set<Lectura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Equipo>(equipo =>
        {
            equipo.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            equipo.Property(e => e.Ubicacion).HasMaxLength(150);
            equipo.Property(e => e.ClaveDispositivo).IsRequired().HasMaxLength(64);

            // Dos equipos no pueden compartir clave: el servidor no sabría de cuál viene la lectura.
            equipo.HasIndex(e => e.ClaveDispositivo).IsUnique();
        });

        modelBuilder.Entity<Lectura>(lectura =>
        {
            // Un equipo tiene muchas lecturas. Restrict impide borrar un equipo
            // que tenga lecturas, protegiendo el historial (RNF07).
            lectura.HasOne(l => l.Equipo)
                   .WithMany(e => e.Lecturas)
                   .HasForeignKey(l => l.EquipoId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Acelera las consultas más frecuentes: última lectura e historial por fechas.
            lectura.HasIndex(l => new { l.EquipoId, l.FechaMedicion });
        });

        // Equipo de prueba para enviar lecturas simuladas mientras no hay hardware.
        // La fecha es fija porque los datos semilla no pueden cambiar entre migraciones.
        modelBuilder.Entity<Equipo>().HasData(new Equipo
        {
            Id = 1,
            Nombre = "Refrigeradora de prueba",
            Tipo = TipoEquipo.Refrigeradora,
            Ubicacion = "Entorno de desarrollo",
            ClaveDispositivo = "clave-prueba-001",
            Activo = true,
            FechaRegistro = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    // Aplica el convertidor UTC a todas las propiedades DateTime del modelo.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
