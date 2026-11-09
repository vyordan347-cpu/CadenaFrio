using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;

namespace CadenaFrio.Tests;

// Hoja del Anexo 10: dos turnos por día (mañana 8–14, tarde 14–20, hora de Perú = UTC-5).
public class HojaControlTemperaturaTests
{
    // Sin rangos guardados rige el de la norma: óptimo 3–7 °C, límite 2–8 °C.
    private static readonly HistoriaRangos Norma = HistoriaRangos.De(Array.Empty<RangoTemperatura>());

    // Hora de Perú del 3 de noviembre de 2026, convertida a UTC.
    private static DateTime Peru(int hora, int minuto = 0) => new DateTime(2026, 11, 3, hora, minuto, 0, DateTimeKind.Utc).AddHours(5);

    private static HojaControlTemperatura.Dia Dia3(params (DateTime, double)[] lecturas) =>
        HojaControlTemperatura.Armar(2026, 11, lecturas, Norma).Single(d => d.Fecha.Day == 3);

    [Fact]
    public void Tiene_una_fila_por_dia_del_mes()
    {
        Assert.Equal(30, HojaControlTemperatura.Armar(2026, 11, Array.Empty<(DateTime, double)>(), Norma).Count);
        Assert.Equal(31, HojaControlTemperatura.Armar(2026, 12, Array.Empty<(DateTime, double)>(), Norma).Count);
    }

    [Fact]
    public void Cada_turno_anota_su_primera_lectura()
    {
        var dia = Dia3((Peru(9, 30), 5.5), (Peru(8, 15), 4.0), (Peru(15), 6.0), (Peru(16), 6.5));

        Assert.Equal(Peru(8, 15), dia.Manana.HoraLectura);   // llegó desordenada: igual toma la primera
        Assert.Equal(4.0, dia.Manana.Temperatura);
        Assert.Equal(2, dia.Manana.Lecturas);
        Assert.Equal(6.0, dia.Tarde.Temperatura);
        Assert.Null(dia.Tarde.Nivel);                        // normal
    }

    [Fact]
    public void Un_turno_sin_lecturas_queda_sin_datos()
    {
        var dia = Dia3((Peru(9), 5.0));

        Assert.True(dia.Manana.HayDatos);
        Assert.False(dia.Tarde.HayDatos);
        Assert.Null(dia.Tarde.Temperatura);
    }

    [Fact]
    public void Avisa_si_salio_del_limite_aunque_la_lectura_anotada_este_bien()
    {
        var dia = Dia3((Peru(8), 5.0), (Peru(11), 9.5));

        Assert.Equal(5.0, dia.Manana.Temperatura);
        Assert.Null(dia.Manana.Nivel);
        Assert.True(dia.Manana.SalioDelLimite);
    }

    [Fact]
    public void Las_lecturas_fuera_de_los_turnos_no_cuentan()
    {
        // Antes de las 8:00 y desde las 20:00 no hay turno en el formato.
        var dia = Dia3((Peru(7, 59), 9.5), (Peru(20), 9.5));

        Assert.False(dia.Manana.HayDatos);
        Assert.False(dia.Tarde.HayDatos);
    }

    [Fact]
    public void La_lectura_de_las_14_es_del_turno_de_la_tarde()
    {
        var dia = Dia3((Peru(14), 7.5));

        Assert.False(dia.Manana.HayDatos);
        Assert.Equal(NivelAlerta.Advertencia, dia.Tarde.Nivel);   // 7.5 °C: fuera del óptimo, dentro del límite
        Assert.False(dia.Tarde.SalioDelLimite);
    }
}
