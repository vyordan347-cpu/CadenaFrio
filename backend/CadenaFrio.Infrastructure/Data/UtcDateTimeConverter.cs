using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CadenaFrio.Infrastructure.Data;

// SQLite guarda las fechas como texto y, al leerlas, pierde la marca de UTC.
// Este convertidor asegura que se guarden en UTC y vuelvan marcadas como UTC.
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            // Al guardar: si la fecha no está en UTC, se convierte.
            fecha => fecha.Kind == DateTimeKind.Utc ? fecha : fecha.ToUniversalTime(),
            // Al leer: se marca como UTC.
            fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc))
    {
    }
}
