using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Notificaciones;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Tests;

// Los servicios anotan en la bitácora lo que hace el personal (Fase 5b).
public class RegistroBitacoraTests : IDisposable
{
    // La vacuna 1 ("Esquema regular (PNI)", 3–7 / 2–8 °C) existe desde los datos semilla.
    private const int VacunaId = 1;

    private readonly BaseDeDatosDePrueba _base = new();
    private readonly GestionRangos _rangos;

    public RegistroBitacoraTests()
    {
        _rangos = new GestionRangos(_base.Db, new Bitacora(_base.Db));
    }

    private Task<List<RegistroAuditoria>> RegistrosAsync() =>
        _base.Db.Bitacora.AsNoTracking().OrderBy(r => r.Id).ToListAsync();

    [Fact]
    public async Task Asignar_vacunas_queda_en_la_bitacora_con_el_rango()
    {
        await _rangos.AsignarVacunasAsync(BaseDeDatosDePrueba.EquipoId, new[] { VacunaId }, "Responsable de prueba");

        var registro = Assert.Single(await RegistrosAsync());
        Assert.Equal(CategoriaAuditoria.Rangos, registro.Categoria);
        Assert.Equal("Asignó vacunas a un equipo", registro.Accion);
        Assert.Equal("Responsable de prueba", registro.Usuario);
        Assert.Contains("Refrigeradora de prueba", registro.Detalle);
        Assert.Contains("Esquema regular (PNI)", registro.Detalle);
    }

    [Fact]
    public async Task Una_asignacion_rechazada_no_queda_en_la_bitacora()
    {
        var resultado = await _rangos.AsignarVacunasAsync(BaseDeDatosDePrueba.EquipoId, new[] { 999 }, "Responsable de prueba");

        Assert.False(resultado.Exito);
        Assert.Empty(await RegistrosAsync());
    }

    [Fact]
    public async Task Recalcular_solo_anota_los_equipos_cuyo_rango_cambia()
    {
        await _rangos.AsignarVacunasAsync(BaseDeDatosDePrueba.EquipoId, new[] { VacunaId }, "Responsable de prueba");

        // Cambia el rango de la vacuna: el equipo que la guarda se recalcula.
        var vacuna = await _base.Db.Vacunas.FirstAsync(v => v.Id == VacunaId);
        vacuna.OptimoMinimo = 4;
        await _base.Db.SaveChangesAsync();
        await _rangos.RecalcularEquiposDeVacunaAsync(VacunaId, "Administrador de prueba");

        // Recalcular otra vez sin cambios no agrega nada.
        await _rangos.RecalcularEquiposDeVacunaAsync(VacunaId, "Administrador de prueba");

        var registros = await RegistrosAsync();
        Assert.Equal(2, registros.Count);
        Assert.Equal("Recalculó el rango de un equipo", registros[1].Accion);
        Assert.Contains("óptimo 4.0–7.0 °C", registros[1].Detalle);
    }

    [Fact]
    public async Task Lo_estoy_atendiendo_queda_en_la_bitacora_una_sola_vez()
    {
        var alerta = new Alerta
        {
            EquipoId = BaseDeDatosDePrueba.EquipoId,
            Tipo = TipoAlerta.TemperaturaAlta,
            Nivel = NivelAlerta.Ruptura,
            Inicio = DateTime.UtcNow.AddMinutes(-5),
            FechaRuptura = DateTime.UtcNow.AddMinutes(-5),
            RevisionRequeridaDesde = DateTime.UtcNow.AddMinutes(-5),
            ValorExtremo = 9.5,
            OptimoMinimo = 3, OptimoMaximo = 7, LimiteMinimo = 2, LimiteMaximo = 8
        };
        _base.Db.Alertas.Add(alerta);
        await _base.Db.SaveChangesAsync();

        // Sin token configurado: no se envían mensajes reales por Telegram.
        var opciones = Options.Create(new OpcionesNotificaciones());
        var reconocimiento = new ReconocimientoAlertas(_base.Db, new ClienteTelegram(new HttpClient(), opciones), new Bitacora(_base.Db));

        await reconocimiento.ReconocerAsync(alerta.Id, "Enfermera de prueba");
        var segundo = await reconocimiento.ReconocerAsync(alerta.Id, "Otra enfermera");

        Assert.False(segundo.Exito);
        var registro = Assert.Single(await RegistrosAsync());
        Assert.Equal(CategoriaAuditoria.Alertas, registro.Categoria);
        Assert.Equal("Enfermera de prueba", registro.Usuario);
        Assert.Contains("desde la web", registro.Detalle);
    }

    public void Dispose() => _base.Dispose();
}
