using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Domain.Valores;
using CadenaFrio.Infrastructure.Data;

namespace CadenaFrio.Infrastructure.Servicios;

// Bitácora de auditoría: anota quién hizo qué y cuándo.
// Se llama desde el mismo lugar donde ocurre cada acción, así el registro explica lo que hizo la persona.
public class Bitacora
{
    private const int LargoAccion = 100;
    private const int LargoDetalle = 500;
    private const int LargoUsuario = 100;

    private readonly CadenaFrioDbContext _db;

    public Bitacora(CadenaFrioDbContext db)
    {
        _db = db;
    }

    // Agrega el registro sin guardarlo: se guarda con el SaveChanges de la acción.
    // Así, si la acción falla, tampoco queda un registro de algo que no ocurrió.
    public void Registrar(CategoriaAuditoria categoria, string accion, string usuario, string? detalle = null)
    {
        _db.Bitacora.Add(new RegistroAuditoria
        {
            Fecha = DateTime.UtcNow,
            Categoria = categoria,
            Accion = Recortar(accion, LargoAccion)!,
            Usuario = Recortar(usuario, LargoUsuario)!,
            Detalle = Recortar(detalle, LargoDetalle)
        });
    }

    // Para acciones que no guardan nada más, como iniciar o cerrar sesión.
    public async Task RegistrarYGuardarAsync(CategoriaAuditoria categoria, string accion, string usuario, string? detalle = null)
    {
        Registrar(categoria, accion, usuario, detalle);
        await _db.SaveChangesAsync();
    }

    // "óptimo 3.0–7.0 °C, límite 2.0–8.0 °C": el mismo formato en todos los registros de rangos.
    public static string DescribirRango(LimitesTemperatura l) =>
        $"óptimo {l.OptimoMinimo:0.0}–{l.OptimoMaximo:0.0} °C, límite {l.LimiteMinimo:0.0}–{l.LimiteMaximo:0.0} °C";

    // Un texto demasiado largo se corta en lugar de impedir la acción.
    private static string? Recortar(string? texto, int largo) =>
        texto is null || texto.Length <= largo ? texto : texto[..(largo - 1)] + "…";
}
