namespace CadenaFrio.Domain.Entities;

// Qué vacunas guarda cada equipo (relación de muchos a muchos).
public class EquipoVacuna
{
    public int EquipoId { get; set; }
    public Equipo Equipo { get; set; } = null!;

    public int VacunaId { get; set; }
    public Vacuna Vacuna { get; set; } = null!;
}
