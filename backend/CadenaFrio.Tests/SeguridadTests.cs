using System.Text.RegularExpressions;
using CadenaFrio.Domain.Valores;

namespace CadenaFrio.Tests;

// Claves de dispositivo y contraseñas temporales.
public class SeguridadTests
{
    [Fact]
    public void La_huella_de_una_clave_es_siempre_la_misma_y_mide_64_caracteres()
    {
        var huella1 = ClavesDispositivo.Hash("clave-prueba-001");
        var huella2 = ClavesDispositivo.Hash("clave-prueba-001");

        Assert.Equal(huella1, huella2);
        Assert.Equal(64, huella1.Length);
        Assert.NotEqual("clave-prueba-001", huella1);   // nunca se guarda la clave original
    }

    [Fact]
    public void Claves_distintas_tienen_huellas_distintas()
    {
        Assert.NotEqual(ClavesDispositivo.Hash("clave-a"), ClavesDispositivo.Hash("clave-b"));
    }

    [Fact]
    public void Las_claves_generadas_son_unicas()
    {
        var claves = Enumerable.Range(0, 100).Select(_ => ClavesDispositivo.Generar()).ToHashSet();

        Assert.Equal(100, claves.Count);
    }

    [Fact]
    public void Las_contrasenas_temporales_no_tienen_caracteres_confusos()
    {
        for (var i = 0; i < 200; i++)
        {
            var contrasena = ContrasenasTemporales.Generar();

            // Formato "Kq7m-82Rx", sin 0, O, 1, l ni I, que se confunden al dictarlas.
            Assert.Matches(new Regex("^[a-km-zA-HJ-NP-Z2-9]{4}-[a-km-zA-HJ-NP-Z2-9]{4}$"), contrasena);
            Assert.Contains(contrasena, char.IsLetter);
            Assert.Contains(contrasena, char.IsDigit);
        }
    }
}
