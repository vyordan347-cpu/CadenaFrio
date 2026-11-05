using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Tests;

// Bitácora de auditoría (Fase 5b): quién hizo qué y cuándo.
public class BitacoraTests
{
    [Fact]
    public async Task Registrar_se_guarda_junto_con_la_accion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var bitacora = new Bitacora(bd.Db);

        bitacora.Registrar(CategoriaAuditoria.Rangos, "Asignó vacunas", "Responsable de prueba", "Refrigeradora: 3–7 °C");

        // Hasta que la acción guarde, no hay nada en la base.
        Assert.Equal(0, await bd.Db.Bitacora.CountAsync());

        await bd.Db.SaveChangesAsync();

        var registro = await bd.Db.Bitacora.SingleAsync();
        Assert.Equal(CategoriaAuditoria.Rangos, registro.Categoria);
        Assert.Equal("Asignó vacunas", registro.Accion);
        Assert.Equal("Responsable de prueba", registro.Usuario);
        Assert.Equal("Refrigeradora: 3–7 °C", registro.Detalle);
        Assert.True(DateTime.UtcNow - registro.Fecha < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Si_la_accion_no_se_guarda_tampoco_queda_registro()
    {
        using var bd = new BaseDeDatosDePrueba();
        var bitacora = new Bitacora(bd.Db);

        bitacora.Registrar(CategoriaAuditoria.Alertas, "Revisó una ruptura", "Enfermera de prueba");
        bd.Db.ChangeTracker.Clear();   // la acción falló y se descartaron sus cambios
        await bd.Db.SaveChangesAsync();

        Assert.Equal(0, await bd.Db.Bitacora.CountAsync());
    }

    [Fact]
    public async Task RegistrarYGuardar_guarda_de_inmediato()
    {
        using var bd = new BaseDeDatosDePrueba();
        var bitacora = new Bitacora(bd.Db);

        await bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Sesion, "Inició sesión", "Enfermera de prueba");

        Assert.Equal(1, await bd.Db.Bitacora.CountAsync());
    }

    [Fact]
    public async Task Un_detalle_muy_largo_se_recorta_sin_impedir_la_accion()
    {
        using var bd = new BaseDeDatosDePrueba();
        var bitacora = new Bitacora(bd.Db);

        await bitacora.RegistrarYGuardarAsync(CategoriaAuditoria.Alertas, "Revisó una ruptura",
            "Enfermera de prueba", new string('x', 800));

        var registro = await bd.Db.Bitacora.SingleAsync();
        Assert.Equal(500, registro.Detalle!.Length);
        Assert.EndsWith("…", registro.Detalle);
    }
}
