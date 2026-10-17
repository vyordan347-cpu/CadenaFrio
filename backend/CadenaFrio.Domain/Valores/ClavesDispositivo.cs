using System.Security.Cryptography;
using System.Text;

namespace CadenaFrio.Domain.Valores;

// Claves con las que cada dispositivo se identifica ante la API (RNF06).
// En la base de datos nunca se guarda la clave, solo su huella (hash SHA-256):
// aunque alguien copie la base, no puede hacerse pasar por un dispositivo.
public static class ClavesDispositivo
{
    // Genera una clave aleatoria de 32 bytes, en texto apto para copiar
    // (letras, números, guion y guion bajo). Imposible de adivinar.
    public static string Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // Huella de la clave: 64 caracteres hexadecimales. Es siempre la misma para
    // la misma clave, así que se puede buscar, pero no se puede revertir.
    // Al ser una clave aleatoria y larga, no necesita sal como una contraseña.
    public static string Hash(string clave) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave))).ToLowerInvariant();
}
