using System.ComponentModel.DataAnnotations;

namespace CadenaFrio.Shared.Contracts;

// Estado de los avisos del usuario con sesión iniciada.
public class MisAvisosDto
{
    public bool TelegramDisponible { get; set; }
    public bool TelegramVinculado { get; set; }
    public string? Correo { get; set; }
    public bool RecibeAlertas { get; set; }
    public bool EsContactoEscalamiento { get; set; }
}

// Enlace para vincular Telegram: abre el bot con un código de un solo uso.
public class EnlaceTelegramDto
{
    public string Enlace { get; set; } = string.Empty;
}

// Configuración de avisos de un usuario, que define el administrador.
public class AvisosUsuarioRequest
{
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [StringLength(150)]
    public string? Correo { get; set; }

    public bool RecibeAlertas { get; set; }
    public bool EsContactoEscalamiento { get; set; }
}

// Alerta urgente activa de un equipo y si alguien ya la está atendiendo.
public class AlertaUrgenteDto
{
    public int Id { get; set; }
    public TipoAlertaDto Tipo { get; set; }
    public string? ReconocidaPor { get; set; }
    public DateTime? FechaReconocimiento { get; set; }
}
