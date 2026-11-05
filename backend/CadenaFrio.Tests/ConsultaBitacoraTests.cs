using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Servicios;

namespace CadenaFrio.Tests;

// Consulta de la bitácora: filtros y páginas (Fase 5b).
public class ConsultaBitacoraTests : IDisposable
{
    private static readonly DateTime Base = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseDeDatosDePrueba _base = new();
    private readonly ConsultaBitacora _consulta;

    public ConsultaBitacoraTests()
    {
        _consulta = new ConsultaBitacora(_base.Db);
    }

    private void Agregar(int dias, string usuario, CategoriaAuditoria categoria, string accion = "Acción")
    {
        _base.Db.Bitacora.Add(new RegistroAuditoria
        {
            Fecha = Base.AddDays(dias),
            Usuario = usuario,
            Categoria = categoria,
            Accion = accion
        });
    }

    [Fact]
    public async Task Sin_filtros_devuelve_todo_con_lo_mas_reciente_primero()
    {
        Agregar(0, "Ana", CategoriaAuditoria.Sesion, "primero");
        Agregar(2, "Ana", CategoriaAuditoria.Sesion, "tercero");
        Agregar(1, "Luis", CategoriaAuditoria.Rangos, "segundo");
        await _base.Db.SaveChangesAsync();

        var resultado = await _consulta.ConsultarAsync(new FiltroBitacora());

        Assert.Equal(3, resultado.Total);
        Assert.Equal(new[] { "tercero", "segundo", "primero" }, resultado.Registros.Select(r => r.Accion));
    }

    [Fact]
    public async Task Filtra_por_fechas_usuario_y_categoria()
    {
        Agregar(0, "Ana", CategoriaAuditoria.Sesion);
        Agregar(1, "Ana", CategoriaAuditoria.Rangos);
        Agregar(2, "Luis", CategoriaAuditoria.Rangos);
        Agregar(5, "Ana", CategoriaAuditoria.Rangos);
        await _base.Db.SaveChangesAsync();

        var resultado = await _consulta.ConsultarAsync(new FiltroBitacora(
            Desde: Base.AddDays(1), Hasta: Base.AddDays(3), Usuario: "Ana", Categoria: CategoriaAuditoria.Rangos));

        var registro = Assert.Single(resultado.Registros);
        Assert.Equal(Base.AddDays(1), registro.Fecha);
        Assert.Equal(1, resultado.Total);
    }

    [Fact]
    public async Task Pagina_de_50_registros()
    {
        for (var i = 0; i < 120; i++)
        {
            Agregar(i, "Ana", CategoriaAuditoria.Sesion, $"r{i}");
        }
        await _base.Db.SaveChangesAsync();

        var primera = await _consulta.ConsultarAsync(new FiltroBitacora(Pagina: 1));
        var tercera = await _consulta.ConsultarAsync(new FiltroBitacora(Pagina: 3));
        var fuera = await _consulta.ConsultarAsync(new FiltroBitacora(Pagina: 4));

        Assert.Equal(120, primera.Total);
        Assert.Equal(50, primera.Registros.Count);
        Assert.Equal("r119", primera.Registros[0].Accion);
        Assert.Equal(20, tercera.Registros.Count);
        Assert.Equal("r0", tercera.Registros[^1].Accion);
        Assert.Empty(fuera.Registros);
    }

    [Fact]
    public async Task Una_pagina_menor_a_1_se_trata_como_la_primera()
    {
        Agregar(0, "Ana", CategoriaAuditoria.Sesion);
        await _base.Db.SaveChangesAsync();

        var resultado = await _consulta.ConsultarAsync(new FiltroBitacora(Pagina: 0));

        Assert.Equal(1, resultado.Pagina);
        Assert.Single(resultado.Registros);
    }

    [Fact]
    public async Task Lista_los_usuarios_sin_repetir_y_en_orden()
    {
        Agregar(0, "Luis", CategoriaAuditoria.Sesion);
        Agregar(1, "Ana", CategoriaAuditoria.Sesion);
        Agregar(2, "Luis", CategoriaAuditoria.Rangos);
        await _base.Db.SaveChangesAsync();

        Assert.Equal(new[] { "Ana", "Luis" }, await _consulta.UsuariosAsync());
    }

    public void Dispose() => _base.Dispose();
}
