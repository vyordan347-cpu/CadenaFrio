namespace CadenaFrio.Infrastructure.Configuracion;

// Datos del establecimiento de salud, configurables en appsettings.json (sección "Establecimiento").
// Se usan para prellenar el formato de notificación de ruptura (Anexo N° 13 de la NTS 242).
public class OpcionesEstablecimiento
{
    public string Nombre { get; set; } = string.Empty;

    // Uno de TiposEstablecimiento, escrito igual, por ejemplo "Puesto de Salud".
    public string Tipo { get; set; } = string.Empty;

    public string DiresaGeresaDiris { get; set; } = string.Empty;
    public string RedSalud { get; set; } = string.Empty;
    public string Microrred { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
    public string TelefonoFijo { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;

    // Casillas de "Tipo de establecimiento" del formato, en su orden.
    public static readonly string[] TiposEstablecimiento =
    {
        "Instituto", "Hospital", "Hospital Materno Infantil", "Centro de Salud", "Puesto de Salud",
        "Almacén Central", "Almacén Regional", "Almacén RED", "Almacén Microrred", "Otros"
    };
}
