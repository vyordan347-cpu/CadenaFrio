using System.Text.Json.Serialization;

namespace CadenaFrio.Shared.Contracts;

// Categoría de un registro de la bitácora. Se envía como texto ("Sesion").
[JsonConverter(typeof(JsonStringEnumConverter<CategoriaAuditoriaDto>))]
public enum CategoriaAuditoriaDto
{
    Sesion = 1,
    Rangos = 2,
    Alertas = 3,
    Administracion = 4
}

// Una línea de la bitácora de auditoría (Fase 5b).
public class RegistroAuditoriaDto
{
    public int Id { get; set; }

    // Cuándo ocurrió (UTC).
    public DateTime Fecha { get; set; }

    public string Usuario { get; set; } = string.Empty;
    public CategoriaAuditoriaDto Categoria { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
}

// Una página de resultados, con los más recientes primero.
public class PaginaBitacoraDto
{
    public List<RegistroAuditoriaDto> Registros { get; set; } = new();
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }

    // Cantidad total de registros que cumplen el filtro (en todas las páginas).
    public int Total { get; set; }

    public int TotalPaginas => TamanoPagina == 0 ? 0 : (Total + TamanoPagina - 1) / TamanoPagina;
}
