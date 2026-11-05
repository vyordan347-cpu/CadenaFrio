using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Tests;

// Respaldos de la base de datos (Fase 5c). Cada prueba usa su propia carpeta temporal.
public class RespaldoBaseDatosTests : IDisposable
{
    private static readonly DateTime Base = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly BaseDeDatosDePrueba _base = new();
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "cadenafrio-pruebas-" + Guid.NewGuid().ToString("N"));
    private readonly RespaldoBaseDatos _respaldos;

    public RespaldoBaseDatosTests()
    {
        _respaldos = new RespaldoBaseDatos(_base.Db,
            Options.Create(new OpcionesRespaldo { Carpeta = _carpeta, Conservar = 7 }));
    }

    [Fact]
    public void El_respaldo_es_una_base_completa_con_los_mismos_datos()
    {
        var archivo = _respaldos.Crear(TipoRespaldo.Manual);

        using var copia = new SqliteConnection($"Data Source={Path.Combine(_carpeta, archivo.Nombre)};Pooling=False");
        copia.Open();
        using var comando = copia.CreateCommand();
        comando.CommandText = "SELECT Nombre FROM Equipos WHERE Id = 1";

        Assert.Equal("Refrigeradora de prueba", comando.ExecuteScalar());
        Assert.StartsWith("manual-", archivo.Nombre);
        Assert.True(archivo.Bytes > 0);
    }

    [Fact]
    public void Solo_se_conservan_los_7_mas_recientes_de_cada_tipo()
    {
        for (var dia = 0; dia < 10; dia++)
        {
            _respaldos.Crear(TipoRespaldo.Automatico, Base.AddDays(dia));
        }
        for (var minuto = 0; minuto < 3; minuto++)
        {
            _respaldos.Crear(TipoRespaldo.Manual, Base.AddMinutes(minuto));
        }

        var lista = _respaldos.Listar();
        var automaticos = lista.Where(a => a.Tipo == TipoRespaldo.Automatico).ToList();

        Assert.Equal(7, automaticos.Count);
        Assert.Equal(Base.AddDays(9), automaticos[0].FechaUtc);   // el más reciente primero
        Assert.Equal(Base.AddDays(3), automaticos[^1].FechaUtc);  // se borraron los días 0, 1 y 2
        // Los manuales no cuentan para el límite de los automáticos.
        Assert.Equal(3, lista.Count(a => a.Tipo == TipoRespaldo.Manual));
    }

    [Fact]
    public void El_dia_del_respaldo_automatico_se_cuenta_en_hora_de_Peru()
    {
        // 04:00 UTC del 30 de octubre son las 23:00 del 29 en Perú.
        _respaldos.Crear(TipoRespaldo.Automatico, new DateTime(2026, 10, 30, 4, 0, 0, DateTimeKind.Utc));

        Assert.True(_respaldos.HayAutomaticoDelDia(new DateOnly(2026, 10, 29)));
        Assert.False(_respaldos.HayAutomaticoDelDia(new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void Un_respaldo_manual_no_cuenta_como_el_automatico_del_dia()
    {
        _respaldos.Crear(TipoRespaldo.Manual, Base);

        Assert.False(_respaldos.HayAutomaticoDelDia(DateOnly.FromDateTime(Base.AddHours(-5))));
    }

    [Fact]
    public void El_automatico_espera_la_hora_configurada_y_se_hace_una_vez_al_dia()
    {
        // HoraDiaria = 3 (por defecto). 07:00 UTC son las 02:00 en Perú: todavía no.
        var dia = new DateTime(2026, 10, 30, 7, 0, 0, DateTimeKind.Utc);

        Assert.Null(_respaldos.CrearAutomaticoSiCorresponde(dia));
        Assert.NotNull(_respaldos.CrearAutomaticoSiCorresponde(dia.AddHours(1)));   // 03:00 en Perú
        Assert.Null(_respaldos.CrearAutomaticoSiCorresponde(dia.AddHours(2)));      // ya se hizo hoy
        // Si la PC estuvo apagada, se hace en cuanto se enciende, aunque sea de tarde.
        Assert.NotNull(_respaldos.CrearAutomaticoSiCorresponde(dia.AddDays(1).AddHours(10)));
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\cadenafrio.db")]
    [InlineData("cadenafrio.db")]
    [InlineData("auto-2026-10-01_030000.db.exe")]
    [InlineData("auto-2026-10-01_999999.db")]   // formato correcto, pero no existe
    public void Solo_se_entregan_respaldos_existentes_con_nombre_valido(string nombre)
    {
        _respaldos.Crear(TipoRespaldo.Automatico, Base);

        Assert.Null(_respaldos.RutaDe(nombre));
    }

    [Fact]
    public void Un_respaldo_existente_se_puede_entregar()
    {
        var archivo = _respaldos.Crear(TipoRespaldo.Automatico, Base);

        Assert.Equal(Path.Combine(_carpeta, archivo.Nombre), _respaldos.RutaDe(archivo.Nombre));
    }

    [Fact]
    public void Los_archivos_ajenos_de_la_carpeta_se_ignoran()
    {
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(Path.Combine(_carpeta, "notas.db"), "no es un respaldo");

        Assert.Empty(_respaldos.Listar());
    }

    public void Dispose()
    {
        _base.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }
}
