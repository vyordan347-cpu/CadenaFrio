using System.Globalization;
using System.Text.RegularExpressions;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Servicios;

public enum TipoRespaldo
{
    Automatico,
    Manual
}

// Un archivo de respaldo en la carpeta. FechaUtc sale del nombre del archivo.
public record ArchivoRespaldo(string Nombre, TipoRespaldo Tipo, DateTime FechaUtc, long Bytes);

// Respaldos de cadenafrio.db (Fase 5c).
// Usa la API de respaldo de SQLite y no una copia del archivo: la base trabaja en modo WAL
// y parte de los datos recientes vive en cadenafrio.db-wal. Esta API copia todo de forma
// consistente, incluso mientras el ESP32 sigue enviando lecturas.
public partial class RespaldoBaseDatos
{
    private const string FormatoFecha = "yyyy-MM-dd_HHmmss";
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    private readonly CadenaFrioDbContext _db;
    private readonly OpcionesRespaldo _opciones;

    public RespaldoBaseDatos(CadenaFrioDbContext db, IOptions<OpcionesRespaldo> opciones)
    {
        _db = db;
        _opciones = opciones.Value;
    }

    // Nombre válido: "auto-2026-10-30_030000.db" o "manual-2026-10-30_154210.db".
    // Solo se aceptan nombres así: evita que se pida otro archivo, como "../appsettings.json".
    [GeneratedRegex(@"^(auto|manual)-(\d{4}-\d{2}-\d{2}_\d{6})\.db$")]
    private static partial Regex NombreValido();

    // Crea el respaldo y luego borra los más antiguos del mismo tipo.
    public ArchivoRespaldo Crear(TipoRespaldo tipo, DateTime? ahoraUtc = null)
    {
        Directory.CreateDirectory(_opciones.Carpeta);

        var horaPeru = (ahoraUtc ?? DateTime.UtcNow) + DesfasePeru;
        var nombre = $"{Prefijo(tipo)}-{horaPeru.ToString(FormatoFecha, CultureInfo.InvariantCulture)}.db";
        var ruta = Path.Combine(_opciones.Carpeta, nombre);

        var origen = (SqliteConnection)_db.Database.GetDbConnection();
        var estabaCerrada = origen.State != System.Data.ConnectionState.Open;
        if (estabaCerrada)
        {
            origen.Open();
        }

        try
        {
            // Pooling=False: la conexión suelta el archivo al cerrarse y se puede borrar después.
            using var destino = new SqliteConnection($"Data Source={ruta};Pooling=False");
            destino.Open();
            origen.BackupDatabase(destino);
        }
        finally
        {
            if (estabaCerrada)
            {
                origen.Close();
            }
        }

        Depurar(tipo);
        return Leer(new FileInfo(ruta))!;
    }

    // Respaldo automático del día: solo si ya pasó la hora configurada (en Perú) y todavía
    // no se hizo hoy. Si la PC estaba apagada a esa hora, se hace en la siguiente revisión.
    // Devuelve el respaldo creado, o null si no correspondía.
    public ArchivoRespaldo? CrearAutomaticoSiCorresponde(DateTime? ahoraUtc = null)
    {
        var ahora = ahoraUtc ?? DateTime.UtcNow;
        var horaPeru = ahora + DesfasePeru;

        if (horaPeru.Hour < _opciones.HoraDiaria || HayAutomaticoDelDia(DateOnly.FromDateTime(horaPeru)))
        {
            return null;
        }

        return Crear(TipoRespaldo.Automatico, ahora);
    }

    // Todos los respaldos, el más reciente primero.
    public List<ArchivoRespaldo> Listar()
    {
        if (!Directory.Exists(_opciones.Carpeta))
        {
            return new();
        }

        return new DirectoryInfo(_opciones.Carpeta)
            .GetFiles("*.db")
            .Select(Leer)
            .OfType<ArchivoRespaldo>()
            .OrderByDescending(a => a.FechaUtc)
            .ToList();
    }

    // ¿Ya hay respaldo automático de este día (en hora de Perú)?
    public bool HayAutomaticoDelDia(DateOnly diaPeru) =>
        Listar().Any(a => a.Tipo == TipoRespaldo.Automatico
                          && DateOnly.FromDateTime(a.FechaUtc + DesfasePeru) == diaPeru);

    // Ruta completa de un respaldo, o null si el nombre no es válido o no existe.
    public string? RutaDe(string nombre)
    {
        if (!NombreValido().IsMatch(nombre))
        {
            return null;
        }

        var ruta = Path.Combine(_opciones.Carpeta, nombre);
        return File.Exists(ruta) ? ruta : null;
    }

    // Conserva los más recientes del tipo indicado y borra el resto.
    private void Depurar(TipoRespaldo tipo)
    {
        var sobrantes = Listar()
            .Where(a => a.Tipo == tipo)
            .Skip(Math.Max(1, _opciones.Conservar));

        foreach (var archivo in sobrantes)
        {
            File.Delete(Path.Combine(_opciones.Carpeta, archivo.Nombre));
        }
    }

    // Lee los datos del nombre. Los archivos con otro nombre se ignoran (no son respaldos).
    private static ArchivoRespaldo? Leer(FileInfo archivo)
    {
        var coincidencia = NombreValido().Match(archivo.Name);
        if (!coincidencia.Success
            || !DateTime.TryParseExact(coincidencia.Groups[2].Value, FormatoFecha, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var horaPeru))
        {
            return null;
        }

        var tipo = coincidencia.Groups[1].Value == "auto" ? TipoRespaldo.Automatico : TipoRespaldo.Manual;
        var utc = DateTime.SpecifyKind(horaPeru - DesfasePeru, DateTimeKind.Utc);

        return new ArchivoRespaldo(archivo.Name, tipo, utc, archivo.Length);
    }

    private static string Prefijo(TipoRespaldo tipo) => tipo == TipoRespaldo.Automatico ? "auto" : "manual";
}
