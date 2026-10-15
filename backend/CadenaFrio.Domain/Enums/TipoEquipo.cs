namespace CadenaFrio.Domain.Enums;

// Tipos de equipo que el sistema puede monitorear.
// Se asignan números explícitos para que, si algún día se agrega
// un tipo nuevo, los valores guardados en la base de datos no cambien.
public enum TipoEquipo
{
    Refrigeradora = 1,
    Termo = 2
}
