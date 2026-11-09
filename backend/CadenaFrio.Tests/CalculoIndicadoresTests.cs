using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;

namespace CadenaFrio.Tests;

// Indicadores en un periodo cualquiera (página de Reportes).
// Rango vigente del equipo 1: el de la NTS por defecto (óptimo 3–7 °C).
public class CalculoIndicadoresTests : IDisposable
{
    // Un día cualquiera, posterior al registro del equipo semilla (14/10/2026).
    private static readonly DateTime Dia = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly BaseDeDatosDePrueba _base = new();
    private readonly CalculoIndicadores _calculo;

    public CalculoIndicadoresTests()
    {
        _calculo = new CalculoIndicadores(_base.Db);
    }

    // Una lectura por minuto desde "inicio".
    private void Lecturas(DateTime inicio, int cantidad, double temperatura, int equipoId = BaseDeDatosDePrueba.EquipoId)
    {
        for (var i = 0; i < cantidad; i++)
        {
            _base.Db.Lecturas.Add(new Lectura
            {
                EquipoId = equipoId,
                Temperatura = temperatura,
                FechaMedicion = inicio.AddMinutes(i),
                FechaRecepcion = inicio.AddMinutes(i)
            });
        }
    }

    private Alerta Ruptura(DateTime inicio, int equipoId = BaseDeDatosDePrueba.EquipoId) => new()
    {
        EquipoId = equipoId,
        Tipo = TipoAlerta.TemperaturaAlta,
        Nivel = NivelAlerta.Ruptura,
        Inicio = inicio,
        FechaRuptura = inicio,
        RevisionRequeridaDesde = inicio,
        Fin = inicio.AddMinutes(30),
        ValorExtremo = 9,
        OptimoMinimo = 3, OptimoMaximo = 7, LimiteMinimo = 2, LimiteMaximo = 8
    };

    [Fact]
    public async Task Un_dia_pasado_solo_cuenta_las_lecturas_de_ese_dia()
    {
        Lecturas(Dia.AddDays(-1), 10, 5);          // el día anterior: no cuenta
        Lecturas(Dia, 30, 5);                      // en óptimo
        Lecturas(Dia.AddHours(1), 30, 7.5);        // fuera del óptimo
        await _base.Db.SaveChangesAsync();

        var r = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(5));

        Assert.Equal(50, r.PorcentajeOptimo);
        // 60 lecturas de las 1440 esperadas ese día: no se cuentan los días posteriores.
        Assert.Equal(Math.Round(100.0 * 60 / 1440, 1), r.PorcentajeDisponibilidad);
    }

    [Fact]
    public async Task Un_periodo_en_curso_espera_lecturas_solo_hasta_ahora()
    {
        Lecturas(Dia, 60, 5);
        await _base.Db.SaveChangesAsync();

        var r = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddHours(1));

        Assert.Equal(100, r.PorcentajeDisponibilidad);
    }

    [Fact]
    public async Task El_periodo_incluye_el_inicio_y_excluye_el_final()
    {
        _base.Db.Alertas.Add(Ruptura(Dia));                 // justo al inicio: cuenta
        _base.Db.Alertas.Add(Ruptura(Dia.AddDays(1)));      // justo al final: es del día siguiente
        await _base.Db.SaveChangesAsync();

        var r = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(5));

        Assert.Equal(1, r.Rupturas);
    }

    [Fact]
    public async Task Se_puede_filtrar_por_equipo()
    {
        var otro = new Equipo { Nombre = "Termo de campaña", Tipo = TipoEquipo.Termo, Activo = true, FechaRegistro = Dia.AddDays(-10) };
        _base.Db.Equipos.Add(otro);
        await _base.Db.SaveChangesAsync();
        _base.Db.Alertas.Add(Ruptura(Dia.AddHours(2), otro.Id));
        await _base.Db.SaveChangesAsync();

        var todos = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(5));
        var soloEquipo1 = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), BaseDeDatosDePrueba.EquipoId, Dia.AddDays(5));
        var soloTermo = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), otro.Id, Dia.AddDays(5));

        Assert.Equal(1, todos.Rupturas);
        Assert.Equal(0, soloEquipo1.Rupturas);
        Assert.Equal(1, soloTermo.Rupturas);
        Assert.Equal(1, soloTermo.PendientesDeRevision);
        Assert.Equal(0, soloEquipo1.PendientesDeRevision);
    }

    [Fact]
    public async Task El_tiempo_de_respuesta_es_el_promedio_de_las_revisiones_del_periodo()
    {
        var primera = Ruptura(Dia.AddHours(1));
        primera.FechaAtencion = primera.RevisionRequeridaDesde!.Value.AddMinutes(10);
        var segunda = Ruptura(Dia.AddHours(5));
        segunda.FechaAtencion = segunda.RevisionRequeridaDesde!.Value.AddMinutes(20);
        _base.Db.Alertas.AddRange(primera, segunda);
        await _base.Db.SaveChangesAsync();

        var r = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(5));

        Assert.Equal(15, r.MinutosPromedioRespuesta);
        Assert.Equal(0, r.PendientesDeRevision);
    }

    [Fact]
    public async Task Sin_lecturas_los_porcentajes_quedan_vacios_y_no_en_cero()
    {
        var r = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(5));

        Assert.Null(r.PorcentajeOptimo);
        Assert.Equal(0, r.PorcentajeDisponibilidad);   // se esperaban lecturas y no llegó ninguna
    }

    [Fact]
    public async Task El_resumen_por_equipo_da_minima_maxima_promedio_y_optimo_del_periodo()
    {
        Lecturas(Dia.AddDays(-1), 5, 20);     // fuera del periodo: no afecta la máxima
        Lecturas(Dia, 2, 4);
        Lecturas(Dia.AddHours(1), 2, 8);      // fuera del óptimo
        _base.Db.Alertas.Add(Ruptura(Dia.AddHours(1)));
        await _base.Db.SaveChangesAsync();

        var fila = Assert.Single(await _calculo.ResumenPorEquipoAsync(Dia, Dia.AddDays(1)));

        Assert.Equal("Refrigeradora de prueba", fila.Nombre);
        Assert.Equal(4, fila.Lecturas);
        Assert.Equal(50, fila.PorcentajeOptimo);
        Assert.Equal(4, fila.Minima);
        Assert.Equal(8, fila.Maxima);
        Assert.Equal(6, fila.Promedio);
        Assert.Equal(1, fila.Rupturas);
    }

    [Fact]
    public async Task Un_equipo_sin_lecturas_aparece_con_cifras_vacias()
    {
        var fila = Assert.Single(await _calculo.ResumenPorEquipoAsync(Dia, Dia.AddDays(1)));

        Assert.Equal(0, fila.Lecturas);
        Assert.Null(fila.PorcentajeOptimo);
        Assert.Null(fila.Minima);
        Assert.Null(fila.Promedio);
    }

    // Rango que rige desde "fecha" para el equipo 1.
    private void CambiarRango(DateTime fecha, double optimoMinimo, double optimoMaximo, double limiteMinimo, double limiteMaximo) =>
        _base.Db.RangosTemperatura.Add(new RangoTemperatura
        {
            EquipoId = BaseDeDatosDePrueba.EquipoId,
            FechaCambio = fecha,
            OptimoMinimo = optimoMinimo, OptimoMaximo = optimoMaximo,
            LimiteMinimo = limiteMinimo, LimiteMaximo = limiteMaximo,
            ModificadoPor = "Prueba"
        });

    [Fact]
    public async Task Cada_lectura_se_juzga_con_el_rango_que_regia_cuando_se_tomo()
    {
        // En la mañana el óptimo era 3–8 °C; al mediodía se cambió a 3–7 °C (el de hoy).
        CambiarRango(Dia, 3, 8, 2, 9);
        CambiarRango(Dia.AddHours(12), 3, 7, 2, 8);
        Lecturas(Dia.AddHours(8), 10, 7.5);    // mañana: dentro del óptimo de ese momento
        Lecturas(Dia.AddHours(14), 10, 7.5);   // tarde: fuera del óptimo nuevo
        await _base.Db.SaveChangesAsync();

        var indicadores = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(2));
        var fila = Assert.Single(await _calculo.ResumenPorEquipoAsync(Dia, Dia.AddDays(1)));

        // Con el rango de hoy daría 0 %; con el de cada momento, la mitad.
        Assert.Equal(50, indicadores.PorcentajeOptimo);
        Assert.Equal(50, fila.PorcentajeOptimo);
    }

    [Fact]
    public async Task La_lectura_justo_en_el_cambio_usa_el_rango_nuevo()
    {
        CambiarRango(Dia, 3, 8, 2, 9);
        CambiarRango(Dia.AddHours(12), 3, 7, 2, 8);
        Lecturas(Dia.AddHours(12), 1, 7.5);    // a las 12:00 en punto ya rige 3–7
        await _base.Db.SaveChangesAsync();

        var indicadores = await _calculo.CalcularAsync(Dia, Dia.AddDays(1), ahoraUtc: Dia.AddDays(2));

        Assert.Equal(0, indicadores.PorcentajeOptimo);
    }

    public void Dispose() => _base.Dispose();
}
