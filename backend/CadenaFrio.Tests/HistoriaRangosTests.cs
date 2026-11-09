using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Servicios;

namespace CadenaFrio.Tests;

// Qué rango regía en cada momento: cada lectura se juzga con el rango que tenía cuando se tomó.
public class HistoriaRangosTests
{
    private static readonly DateTime Lunes = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private static readonly LimitesTemperatura Amplio = new(3, 7, 2, 8);
    private static readonly LimitesTemperatura Estrecho = new(4, 6, 3, 7);

    private static RangoTemperatura Rango(int id, DateTime fecha, LimitesTemperatura l) => new()
    {
        Id = id,
        EquipoId = 1,
        FechaCambio = fecha,
        OptimoMinimo = l.OptimoMinimo,
        OptimoMaximo = l.OptimoMaximo,
        LimiteMinimo = l.LimiteMinimo,
        LimiteMaximo = l.LimiteMaximo
    };

    [Fact]
    public void Sin_rangos_guardados_rige_el_de_la_norma()
    {
        var historia = HistoriaRangos.De(Array.Empty<RangoTemperatura>());

        Assert.Equal(LimitesTemperatura.PorDefecto, historia.En(Lunes));
    }

    [Fact]
    public void Antes_del_primer_rango_rige_el_de_la_norma()
    {
        var historia = HistoriaRangos.De(new[] { Rango(1, Lunes.AddDays(2), Estrecho) });

        Assert.Equal(LimitesTemperatura.PorDefecto, historia.En(Lunes));
        Assert.Equal(Estrecho, historia.En(Lunes.AddDays(3)));
    }

    [Fact]
    public void Cada_fecha_usa_el_ultimo_rango_definido_antes()
    {
        var historia = HistoriaRangos.De(new[]
        {
            Rango(2, Lunes.AddDays(2), Estrecho),   // se pasan desordenados a propósito
            Rango(1, Lunes, Amplio)
        });

        Assert.Equal(Amplio, historia.En(Lunes.AddDays(1)));
        Assert.Equal(Estrecho, historia.En(Lunes.AddDays(2)));   // justo en el cambio ya rige el nuevo
        Assert.Equal(Estrecho, historia.En(Lunes.AddDays(5)));
    }

    [Fact]
    public void Dos_cambios_a_la_misma_hora_vale_el_ultimo_guardado()
    {
        var historia = HistoriaRangos.De(new[]
        {
            Rango(1, Lunes, Amplio),
            Rango(2, Lunes, Estrecho)
        });

        Assert.Equal(Estrecho, historia.En(Lunes.AddHours(1)));
    }

    [Fact]
    public void Sin_cambios_en_el_periodo_hay_un_solo_tramo()
    {
        var historia = HistoriaRangos.De(new[] { Rango(1, Lunes, Amplio) });

        var tramo = Assert.Single(historia.Tramos(Lunes.AddDays(1), Lunes.AddDays(3)));
        Assert.Equal(new HistoriaRangos.Tramo(Lunes.AddDays(1), Lunes.AddDays(3), Amplio), tramo);
    }

    [Fact]
    public void Un_cambio_a_mitad_del_periodo_parte_en_dos_tramos()
    {
        var historia = HistoriaRangos.De(new[]
        {
            Rango(1, Lunes, Amplio),
            Rango(2, Lunes.AddDays(2), Estrecho)
        });

        var tramos = historia.Tramos(Lunes.AddDays(1), Lunes.AddDays(3));

        Assert.Equal(new[]
        {
            new HistoriaRangos.Tramo(Lunes.AddDays(1), Lunes.AddDays(2), Amplio),
            new HistoriaRangos.Tramo(Lunes.AddDays(2), Lunes.AddDays(3), Estrecho)
        }, tramos);
    }

    [Fact]
    public void Tramos_cubren_el_periodo_sin_huecos_ni_tramos_vacios()
    {
        var historia = HistoriaRangos.De(new[]
        {
            Rango(1, Lunes.AddHours(12), Estrecho),
            Rango(2, Lunes.AddHours(12), Amplio),      // dos cambios a la misma hora
            Rango(3, Lunes.AddHours(18), Amplio)       // el mismo rango guardado de nuevo: no es un cambio
        });

        var tramos = historia.Tramos(Lunes, Lunes.AddDays(1));

        // Antes de las 12:00 rige el de la norma (igual a "Amplio"), y después "Amplio": un solo tramo.
        var tramo = Assert.Single(tramos);
        Assert.Equal(Lunes, tramo.Desde);
        Assert.Equal(Lunes.AddDays(1), tramo.Hasta);
    }

    [Fact]
    public void Cada_lectura_se_cuenta_con_el_rango_de_su_momento()
    {
        // Hasta el miércoles rige el rango amplio (óptimo 3–7, límite 2–8); desde ahí, el estrecho (4–6, 3–7).
        var historia = HistoriaRangos.De(new[]
        {
            Rango(1, Lunes, Amplio),
            Rango(2, Lunes.AddDays(2), Estrecho)
        });

        var conteo = historia.Contar(new[]
        {
            (Lunes.AddDays(1), 6.5),   // martes: en el óptimo del amplio (con el estrecho no lo estaría)
            (Lunes.AddDays(1), 7.5),   // martes: advertencia con el amplio, no ruptura
            (Lunes.AddDays(3), 6.5),   // jueves: advertencia con el estrecho
            (Lunes.AddDays(3), 7.5),   // jueves: ruptura con el estrecho (límite 7)
            (Lunes.AddDays(3), 5.0)    // jueves: en el óptimo
        });

        Assert.Equal(new HistoriaRangos.Conteo(EnOptimo: 2, FueraDelLimite: 1), conteo);
    }

    [Fact]
    public void Los_bordes_del_rango_cuentan_como_dentro()
    {
        var historia = HistoriaRangos.De(new[] { Rango(1, Lunes, Amplio) });

        var conteo = historia.Contar(new[] { (Lunes.AddHours(1), 3.0), (Lunes.AddHours(2), 7.0), (Lunes.AddHours(3), 8.0) });

        // 3 y 7 están en el óptimo; 8 está fuera del óptimo pero no del límite.
        Assert.Equal(new HistoriaRangos.Conteo(EnOptimo: 2, FueraDelLimite: 0), conteo);
    }

    [Fact]
    public void Rango_antiguo_sin_optimo_usa_su_limite_como_optimo()
    {
        // Así quedaron algunos rangos guardados antes de la migración DosNiveles: óptimo 0–0.
        var antiguo = new RangoTemperatura
        {
            Id = 1, EquipoId = 1, FechaCambio = Lunes,
            OptimoMinimo = 0, OptimoMaximo = 0, LimiteMinimo = 2.4, LimiteMaximo = 8
        };

        var historia = HistoriaRangos.De(new[] { antiguo });

        Assert.Equal(new LimitesTemperatura(2.4, 8, 2.4, 8), historia.En(Lunes.AddHours(1)));
        Assert.Equal(new HistoriaRangos.Conteo(EnOptimo: 1, FueraDelLimite: 0), historia.Contar(new[] { (Lunes.AddHours(1), 5.0) }));
    }

    [Fact]
    public void Periodo_vacio_no_tiene_tramos()
    {
        var historia = HistoriaRangos.De(new[] { Rango(1, Lunes, Amplio) });

        Assert.Empty(historia.Tramos(Lunes, Lunes));
    }
}
