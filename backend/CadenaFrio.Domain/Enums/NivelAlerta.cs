namespace CadenaFrio.Domain.Enums;

// Gravedad de una alerta de temperatura, según la Directiva Sanitaria N° 133-MINSA-2021.
public enum NivelAlerta
{
    // Fuera del rango óptimo pero dentro del límite: revisar el termostato.
    Advertencia = 1,

    // Fuera del rango límite: ruptura de la cadena de frío, activar el plan de contingencia.
    Ruptura = 2
}
