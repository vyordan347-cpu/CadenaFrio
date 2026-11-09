using CadenaFrio.Infrastructure.Notificaciones;

namespace CadenaFrio.Tests;

// Texto del resumen diario por Telegram: solo hechos, en palabras simples.
public class MensajeResumenDiarioTests
{
    private static readonly DateOnly Dia = new(2026, 11, 8);
    private static readonly DateTime Ahora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc); // 07:00 de Perú

    private static readonly EquipoDelResumen Normal = new("Refrigeradora 1", 1380, 1440, 3.0, 9.0, 96.0, 1);

    private static string Armar(IReadOnlyList<EquipoDelResumen> equipos, IReadOnlyList<PendienteDelResumen>? pendientes = null) =>
        MensajeResumenDiario.Armar(Dia, "Posta X", equipos, pendientes ?? [], Ahora);

    [Fact]
    public void Muestra_las_cifras_de_cada_equipo()
    {
        var texto = Armar([Normal]);

        Assert.Contains("Resumen del 08/11/2026</b> — Posta X", texto);
        Assert.Contains("<b>Refrigeradora 1</b>", texto);
        Assert.Contains("Mín 3 °C · Máx 9 °C", texto);
        Assert.Contains("96 % de las lecturas en rango óptimo", texto);
        Assert.Contains("Lecturas: 1 380 de 1 440 esperadas", texto);
        Assert.Contains("1 ruptura de temperatura", texto);
    }

    [Fact]
    public void Un_equipo_sin_lecturas_lo_dice()
    {
        var texto = Armar([new EquipoDelResumen("Termo 2", 0, 1440, null, null, null, 0)]);

        Assert.Contains("Sin lecturas en todo el día", texto);
        Assert.DoesNotContain("Mín", texto);
        Assert.DoesNotContain("ruptura", texto);
    }

    [Fact]
    public void Nunca_afirma_que_las_vacunas_esten_seguras()
    {
        var texto = Armar([Normal with { Rupturas = 0, PorcentajeOptimo = 100 }]);

        Assert.DoesNotContain("segur", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sin revisiones pendientes", texto);
    }

    [Fact]
    public void Lista_las_pendientes_con_su_plazo()
    {
        var plazo = new DateTime(2026, 11, 9, 19, 20, 0, DateTimeKind.Utc); // 14:20 de Perú
        var vencido = new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);

        var texto = Armar([Normal],
        [
            new("Refrigeradora 1", "ruptura", plazo),
            new("Termo 2", "ruptura", vencido),
            new("Termo 2", "sin señal", null)
        ]);

        Assert.Contains("Revisiones pendientes: 3", texto);
        Assert.Contains("Refrigeradora 1, ruptura: notificar antes del 09/11 14:20", texto);
        Assert.Contains("Termo 2, ruptura: plazo vencido el 08/11 05:00", texto);
        Assert.Contains("Termo 2, sin señal\n", texto);
    }

    [Fact]
    public void Escapa_los_nombres_para_el_html_de_Telegram()
    {
        var texto = Armar([Normal with { Nombre = "Refri <A&B>" }]);

        Assert.Contains("Refri &lt;A&amp;B&gt;", texto);
    }

    [Fact]
    public void El_dia_en_curso_dice_hasta_que_hora()
    {
        var hoy = MensajeResumenDiario.Armar(new DateOnly(2026, 11, 9), "Posta X", [Normal], [], Ahora);

        Assert.Contains("Resumen del 09/11/2026</b> (hasta las 07:00) — Posta X", hoy);
        Assert.DoesNotContain("hasta las", Armar([Normal]));
    }

    [Fact]
    public void Sin_equipos_activos_lo_dice()
    {
        Assert.Contains("No hay equipos activos", Armar([]));
    }
}
