using CadenaFrio.Domain.Enums;

namespace CadenaFrio.Domain.Entities;

// Situación que requiere atención del personal (HU03, RF06, RF08).
// Se abre con la primera lectura fuera de rango y se cierra cuando la temperatura vuelve al rango.
public class Alerta
{
    public int Id { get; set; }

    public int EquipoId { get; set; }
    public Equipo Equipo { get; set; } = null!;

    public TipoAlerta Tipo { get; set; }

    // Hora de medición de la primera lectura fuera de rango (UTC).
    public DateTime Inicio { get; set; }

    // Hora en que la temperatura volvió al rango (UTC). Nulo mientras la alerta siga activa.
    public DateTime? Fin { get; set; }

    // La temperatura más alejada del rango durante la alerta (el pico más alto o el más bajo).
    public double ValorExtremo { get; set; }

    // Rango vigente cuando se abrió la alerta. Se guarda porque el rango puede cambiar
    // después, y la alerta debe seguir explicando por qué se abrió.
    public double RangoMinimo { get; set; }
    public double RangoMaximo { get; set; }
}
