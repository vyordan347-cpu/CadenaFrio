using System.Security.Cryptography;

namespace CadenaFrio.Domain.Valores;

// Genera contraseñas temporales fáciles de dictar o copiar, por ejemplo "Kq7m-82Rx".
// Se excluyen caracteres que se confunden al leer: 0/O, 1/l/I.
public static class ContrasenasTemporales
{
    private const string Letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digitos = "23456789";
    private const string Todos = Letras + Digitos;

    public static string Generar()
    {
        // Dos bloques de 4 caracteres; se garantiza al menos una letra y un dígito.
        var caracteres = new char[8];
        caracteres[0] = Letras[RandomNumberGenerator.GetInt32(Letras.Length)];
        caracteres[1] = Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)];
        for (var i = 2; i < caracteres.Length; i++)
        {
            caracteres[i] = Todos[RandomNumberGenerator.GetInt32(Todos.Length)];
        }

        RandomNumberGenerator.Shuffle(caracteres.AsSpan());
        return $"{new string(caracteres, 0, 4)}-{new string(caracteres, 4, 4)}";
    }
}
