using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Infrastructure.Servicios;

// "Hoja de control y registro diario de la temperatura de refrigeración" (Anexo N° 10 de la
// NTS N° 242-MINSA/DGIESP-2026), armada con las lecturas del sensor.
//
// En papel, el personal anota la temperatura dos veces al día: en el turno de la mañana
// (8:00 a 14:00) y en el de la tarde (14:00 a 20:00). Aquí, para cada turno se toma la primera
// lectura del turno (como quien controla el termómetro al empezar), y además se indica si en
// algún momento del turno la temperatura salió del límite, para que no se esconda nada.
// Cada lectura se juzga con el rango que regía cuando se tomó (HistoriaRangos).
public static class HojaControlTemperatura
{
    private static readonly TimeSpan DesfasePeru = TimeSpan.FromHours(-5);

    // Turnos del formato, en hora de Perú: [inicio, fin).
    public static readonly TimeSpan InicioManana = TimeSpan.FromHours(8);
    public static readonly TimeSpan InicioTarde = TimeSpan.FromHours(14);
    public static readonly TimeSpan FinTarde = TimeSpan.FromHours(20);

    // Lo que se anota en un turno. Sin lecturas en el turno: HayDatos = false y lo demás vacío.
    public sealed record Turno(
        bool HayDatos,
        DateTime? HoraLectura,          // UTC de la lectura anotada
        double? Temperatura,            // la lectura anotada
        NivelAlerta? Nivel,             // de la lectura anotada: null (normal), advertencia o ruptura
        bool SalioDelLimite,            // alguna lectura del turno estuvo fuera del límite
        int Lecturas);                  // cuántas lecturas hubo en el turno

    public sealed record Dia(DateOnly Fecha, Turno Manana, Turno Tarde);

    // El mes completo [inicio, fin) en UTC, para consultar las lecturas.
    public static (DateTime Desde, DateTime Hasta) PeriodoUtc(int anio, int mes)
    {
        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc) - DesfasePeru;
        return (inicio, inicio.AddMonths(1));
    }

    // Una fila por día del mes, con sus dos turnos. "lecturas" puede venir desordenada.
    public static List<Dia> Armar(int anio, int mes, IEnumerable<(DateTime Fecha, double Temperatura)> lecturas, HistoriaRangos historia)
    {
        var ordenadas = lecturas.OrderBy(l => l.Fecha).ToList();
        var dias = new List<Dia>();
        for (var d = 1; d <= DateTime.DaysInMonth(anio, mes); d++)
        {
            var fecha = new DateOnly(anio, mes, d);
            var medianocheUtc = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DesfasePeru;
            dias.Add(new Dia(
                fecha,
                ArmarTurno(ordenadas, medianocheUtc + InicioManana, medianocheUtc + InicioTarde, historia),
                ArmarTurno(ordenadas, medianocheUtc + InicioTarde, medianocheUtc + FinTarde, historia)));
        }
        return dias;
    }

    private static Turno ArmarTurno(List<(DateTime Fecha, double Temperatura)> ordenadas, DateTime desde, DateTime hasta, HistoriaRangos historia)
    {
        var delTurno = ordenadas.Where(l => l.Fecha >= desde && l.Fecha < hasta).ToList();
        if (delTurno.Count == 0)
        {
            return new Turno(false, null, null, null, false, 0);
        }

        var primera = delTurno[0];
        var salio = delTurno.Any(l => historia.En(l.Fecha).Clasificar(l.Temperatura).Nivel == NivelAlerta.Ruptura);
        return new Turno(
            true,
            primera.Fecha,
            primera.Temperatura,
            historia.En(primera.Fecha).Clasificar(primera.Temperatura).Nivel,
            salio,
            delTurno.Count);
    }
}
