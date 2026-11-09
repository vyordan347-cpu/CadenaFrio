using CadenaFrio.Shared.Utilidades;

namespace CadenaFrio.Tests;

// Código QR de cada equipo: el enlace que lleva y la imagen.
public class EtiquetaEquipoTests
{
    [Theory]
    [InlineData("http://192.168.1.20:5067", "http://192.168.1.20:5067/equipos/3")]
    [InlineData("http://192.168.1.20:5067/", "http://192.168.1.20:5067/equipos/3")]   // con barra al final
    [InlineData(" http://posta.local:5067 ", "http://posta.local:5067/equipos/3")]     // con espacios
    public void El_enlace_lleva_al_detalle_del_equipo(string direccion, string esperado)
    {
        Assert.Equal(esperado, EtiquetaEquipo.Enlace(direccion, 3));
    }

    [Theory]
    [InlineData("http://localhost:5067", true)]
    [InlineData("http://127.0.0.1:5067", true)]
    [InlineData("http://192.168.1.20:5067", false)]
    [InlineData("no es una dirección", false)]
    public void Reconoce_las_direcciones_que_no_sirven_en_el_celular(string direccion, bool esLocal)
    {
        Assert.Equal(esLocal, EtiquetaEquipo.EsLocal(direccion));
    }

    [Fact]
    public void Genera_una_imagen_svg()
    {
        var svg = EtiquetaEquipo.Svg("http://192.168.1.20:5067/equipos/3");

        Assert.StartsWith("<svg", svg.TrimStart());
        Assert.Contains("rect", svg);   // los módulos del QR
    }
}
