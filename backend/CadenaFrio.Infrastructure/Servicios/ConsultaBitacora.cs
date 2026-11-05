using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

// Filtros de la bitácora. Todos son opcionales; las fechas van en UTC.
public record FiltroBitacora(
    DateTime? Desde = null,
    DateTime? Hasta = null,
    string? Usuario = null,
    CategoriaAuditoria? Categoria = null,
    int Pagina = 1);

public record PaginaBitacora(List<RegistroAuditoria> Registros, int Pagina, int Total);

// Consulta de la bitácora para la pestaña de Administración. Solo lee: nunca modifica registros.
public class ConsultaBitacora
{
    public const int TamanoPagina = 50;

    private readonly CadenaFrioDbContext _db;

    public ConsultaBitacora(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // Los más recientes primero. Una página fuera de rango devuelve una lista vacía.
    public async Task<PaginaBitacora> ConsultarAsync(FiltroBitacora filtro)
    {
        var consulta = _db.Bitacora.AsNoTracking();

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(r => r.Fecha >= desde);
        }
        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(r => r.Fecha <= hasta);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Usuario))
        {
            consulta = consulta.Where(r => r.Usuario == filtro.Usuario);
        }
        if (filtro.Categoria is { } categoria)
        {
            consulta = consulta.Where(r => r.Categoria == categoria);
        }

        var pagina = Math.Max(1, filtro.Pagina);
        var total = await consulta.CountAsync();
        var registros = await consulta
            .OrderByDescending(r => r.Fecha)
            .ThenByDescending(r => r.Id)
            .Skip((pagina - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .ToListAsync();

        return new(registros, pagina, total);
    }

    // Nombres que aparecen en la bitácora, para el filtro por usuario.
    // Incluye los nombres escritos en intentos fallidos de inicio de sesión.
    public Task<List<string>> UsuariosAsync() =>
        _db.Bitacora.AsNoTracking()
            .Select(r => r.Usuario)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync();
}
