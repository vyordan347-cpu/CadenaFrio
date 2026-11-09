using QRCoder;

namespace CadenaFrio.Shared.Utilidades;

// Código QR que se pega en cada equipo: al escanearlo con el celular se abre el detalle de ese
// equipo (temperatura en vivo, estado y alertas). Se genera en el navegador, sin internet,
// con QRCoder (licencia MIT).
public static class EtiquetaEquipo
{
    // Enlace que lleva el QR, por ejemplo "http://192.168.1.20:5067/equipos/3".
    // "direccion" es cómo llega el CELULAR a la PC del servidor (no "localhost").
    public static string Enlace(string direccion, int equipoId) =>
        $"{direccion.Trim().TrimEnd('/')}/equipos/{equipoId}";

    // ¿La dirección solo funciona en la misma PC? Con ella el QR no sirve en el celular.
    public static bool EsLocal(string direccion)
    {
        if (!Uri.TryCreate(direccion.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }
        return uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }

    // El QR como imagen SVG (se ve nítido al imprimir). Corrección de errores media: se sigue
    // leyendo aunque la etiqueta se ensucie o se raye un poco.
    public static string Svg(string texto)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(datos).GetGraphic(8);
    }
}
