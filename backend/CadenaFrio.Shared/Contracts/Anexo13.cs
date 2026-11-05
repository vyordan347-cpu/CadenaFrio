namespace CadenaFrio.Shared.Contracts;

// Una opción de las listas del Anexo N° 13 (NTS 242), para mostrarla en la web.
public class OpcionAnexo13Dto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    // Encabezado bajo el que se agrupa, por ejemplo "Falla en el suministro de energía".
    public string? Grupo { get; set; }

    // "Otro" y "Otra causa" obligan a escribir cuál fue.
    public bool RequiereDetalle { get; set; }
}

// Las tres listas del formato. La web las pide a la API para no copiarlas.
public class CatalogoAnexo13Dto
{
    public List<OpcionAnexo13Dto> CausasRefrigerador { get; set; } = new();
    public List<OpcionAnexo13Dto> CausasTermo { get; set; } = new();
    public List<OpcionAnexo13Dto> FormasRestablecimiento { get; set; } = new();
}
