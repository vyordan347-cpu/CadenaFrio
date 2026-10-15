using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Valores;

// Los dos rangos que se aplican a un equipo y la regla para clasificar una temperatura.
// Es lógica pura del dominio: no depende de la base de datos ni de la web,
// por eso es fácil de entender, reutilizar y probar.
public record LimitesTemperatura(
    double OptimoMinimo,
    double OptimoMaximo,
    double LimiteMinimo,
    double LimiteMaximo)
{
    // Valores por defecto (RF04), según la Directiva Sanitaria N° 133-MINSA-2021:
    // normal de 2 a 7 °C; ruptura por debajo de 0 °C o por encima de 8 °C.
    public static LimitesTemperatura PorDefecto { get; } = new(2.0, 7.0, 0.0, 8.0);

    // Devuelve la dirección y el nivel de alerta que corresponde a una temperatura,
    // o (null, null) si está dentro del rango óptimo.
    public (TipoAlerta? Tipo, NivelAlerta? Nivel) Clasificar(double temperatura)
    {
        TipoAlerta? tipo =
            temperatura > OptimoMaximo ? TipoAlerta.TemperaturaAlta :
            temperatura < OptimoMinimo ? TipoAlerta.TemperaturaBaja :
            null;

        if (tipo is null)
        {
            return (null, null);
        }

        var esRuptura = temperatura > LimiteMaximo || temperatura < LimiteMinimo;
        return (tipo, esRuptura ? NivelAlerta.Ruptura : NivelAlerta.Advertencia);
    }
}
