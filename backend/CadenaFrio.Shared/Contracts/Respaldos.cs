namespace CadenaFrio.Shared.Contracts;

// Un respaldo de la base de datos en la lista de Administración (Fase 5c).
public class RespaldoDto
{
    // Nombre del archivo, por ejemplo "auto-2026-10-30_030000.db". Sirve para descargarlo.
    public string Nombre { get; set; } = string.Empty;

    // Cuándo se hizo (UTC).
    public DateTime Fecha { get; set; }

    public bool Automatico { get; set; }
    public long Bytes { get; set; }
}
