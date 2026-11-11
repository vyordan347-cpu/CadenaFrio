using CadenaFrio.Shared.Contracts;
using CadenaFrio.Shared.Utilidades;

namespace CadenaFrio.Tests;

// Selección de vacunas en Rangos de conservación: se bloquea la que no se cruza con las marcadas.
public class CompatibilidadVacunasTests
{
    private static VacunaDto Vacuna(int id, string nombre, double optMin, double optMax) => new()
    {
        Id = id, Nombre = nombre, OptimoMinimo = optMin, OptimoMaximo = optMax,
        LimiteMinimo = optMin - 1, LimiteMaximo = optMax + 1, Activo = true
    };

    private static readonly VacunaDto Pni = Vacuna(1, "Esquema regular (PNI)", 3, 7);
    private static readonly VacunaDto Importada = Vacuna(2, "Vacuna importada", 2, 6);
    private static readonly VacunaDto VacunaX = Vacuna(3, "Vacuna X", 10, 15);

    [Fact]
    public void Sin_vacunas_marcadas_todas_se_pueden_marcar()
    {
        Assert.Null(CompatibilidadVacunas.Motivo(VacunaX, []));
    }

    [Fact]
    public void Una_vacuna_con_temperatura_en_comun_se_puede_marcar()
    {
        Assert.Null(CompatibilidadVacunas.Motivo(Importada, [Pni]));
    }

    [Fact]
    public void Una_vacuna_sin_temperatura_en_comun_se_bloquea_y_dice_por_que()
    {
        var motivo = CompatibilidadVacunas.Motivo(VacunaX, [Pni]);

        Assert.NotNull(motivo);
        Assert.Equal("Su óptimo (10.0–15.0 °C) no se cruza con Esquema regular (PNI) (3.0–7.0 °C): " +
                     "no hay una temperatura que sirva para todas.", motivo);
    }

    [Fact]
    public void Tocarse_en_un_punto_no_basta()
    {
        // Igual que la API: el óptimo común debe tener un tramo, no un solo valor.
        Assert.NotNull(CompatibilidadVacunas.Motivo(Vacuna(4, "Borde", 7, 9), [Pni]));
    }

    [Fact]
    public void Nombra_solo_las_vacunas_que_chocan()
    {
        // 2.5–3.5 se cruza con el PNI (3–7) y con la importada (2–6): ningún conflicto.
        Assert.Empty(CompatibilidadVacunas.Conflictos(Vacuna(5, "Fría", 2.5, 3.5), [Pni, Importada]));

        // 1–2.9 se cruza con la importada (2–6), pero no con el PNI (3–7): solo se nombra el PNI.
        Assert.Equal(["Esquema regular (PNI)"],
            CompatibilidadVacunas.Conflictos(Vacuna(6, "Muy fría", 1, 2.9), [Pni, Importada]).Select(c => c.Nombre));
    }
}
