using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
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
    public DbSet<RangoTemperatura> RangosTemperatura => Set<RangoTemperatura>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Equipo>(equipo =>
        {
            equipo.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            equipo.Property(e => e.Ubicacion).HasMaxLength(150);
            // Guarda la huella (hash SHA-256) de la clave, no la clave: 64 caracteres.
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
            // Además es único: un equipo no puede tener dos lecturas medidas en el mismo
            // instante. Si el dispositivo reenvía un lote, la base rechaza los duplicados (HU06).
            lectura.HasIndex(l => new { l.EquipoId, l.FechaMedicion }).IsUnique();

            // Para que el vigilante encuentre rápido cuándo se recibió el último dato de cada equipo.
            lectura.HasIndex(l => new { l.EquipoId, l.FechaRecepcion });
        });

        modelBuilder.Entity<RangoTemperatura>(rango =>
        {
            rango.Property(r => r.ModificadoPor).IsRequired().HasMaxLength(100);

            // El rango límite conserva los nombres de columna originales, para que la
            // migración solo agregue columnas nuevas y no se pierdan los datos existentes.
            rango.Property(r => r.LimiteMinimo).HasColumnName("TemperaturaMinima");
            rango.Property(r => r.LimiteMaximo).HasColumnName("TemperaturaMaxima");

            // Un equipo tiene muchos rangos (el vigente y su historial).
            // Restrict protege el historial de cambios igual que con las lecturas.
            rango.HasOne(r => r.Equipo)
                 .WithMany(e => e.Rangos)
                 .HasForeignKey(r => r.EquipoId)
                 .OnDelete(DeleteBehavior.Restrict);

            // Permite encontrar rápido el rango vigente (el más reciente de cada equipo).
            rango.HasIndex(r => new { r.EquipoId, r.FechaCambio });

            // Regla en la propia base de datos: el límite mínimo debe ser menor que el máximo.
            // Aunque alguien inserte datos sin pasar por la API, SQLite lo rechazará.
            // (La relación entre el rango óptimo y el límite se valida en la API y en la web.)
            rango.ToTable(t => t.HasCheckConstraint(
                "CK_RangoTemperatura_MinimaMenorQueMaxima",
                "\"TemperaturaMinima\" < \"TemperaturaMaxima\""));
        });

        modelBuilder.Entity<Alerta>(alerta =>
        {
            // Igual que en los rangos: se conservan los nombres de columna originales.
            alerta.Property(a => a.LimiteMinimo).HasColumnName("RangoMinimo");
            alerta.Property(a => a.LimiteMaximo).HasColumnName("RangoMaximo");

            alerta.Property(a => a.AtendidaPor).HasMaxLength(100);
            alerta.Property(a => a.ObservacionAtencion).HasMaxLength(500);

            // El historial de alertas también se protege contra borrados.
            alerta.HasOne(a => a.Equipo)
                  .WithMany(e => e.Alertas)
                  .HasForeignKey(a => a.EquipoId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Regla en la base de datos: un equipo no puede tener dos alertas
            // abiertas del mismo tipo al mismo tiempo. El filtro hace que el índice
            // único solo considere las alertas activas (Fin vacío).
            alerta.HasIndex(a => new { a.EquipoId, a.Tipo })
                  .IsUnique()
                  .HasFilter("\"Fin\" IS NULL");

            // Para consultar el historial de alertas por fechas (Sprint 5).
            alerta.HasIndex(a => new { a.EquipoId, a.Inicio });
        });

        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.Property(u => u.NombreCompleto).IsRequired().HasMaxLength(100);
            usuario.Property(u => u.NombreUsuario).IsRequired().HasMaxLength(50);
            usuario.Property(u => u.ContrasenaHash).IsRequired();
            usuario.Property(u => u.SelloSeguridad).IsRequired().HasMaxLength(64);

            // Dos personas no pueden tener el mismo nombre de usuario.
            usuario.HasIndex(u => u.NombreUsuario).IsUnique();
        });

        // Los usuarios de prueba no se crean aquí, sino al iniciar la API
        // (SembradorUsuarios), porque el cifrado de contraseñas genera un valor
        // distinto cada vez y los datos semilla de una migración deben ser fijos.

        // Equipo de prueba para enviar lecturas simuladas mientras no hay hardware.
        // La fecha es fija porque los datos semilla no pueden cambiar entre migraciones.
        modelBuilder.Entity<Equipo>().HasData(new Equipo
        {
            Id = 1,
            Nombre = "Refrigeradora de prueba",
            Tipo = TipoEquipo.Refrigeradora,
            Ubicacion = "Entorno de desarrollo",
            // Se guarda la huella de "clave-prueba-001": el ESP32 y el simulador
            // siguen enviando esa clave, y la API compara huellas.
            ClaveDispositivo = ClavesDispositivo.Hash("clave-prueba-001"),
            Activo = true,
            FechaRegistro = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    // Aplica el convertidor UTC a todas las propiedades DateTime del modelo
    // (también a las que aceptan nulos, como Alerta.Fin).
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
