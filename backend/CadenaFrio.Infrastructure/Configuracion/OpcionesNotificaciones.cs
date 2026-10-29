namespace CadenaFrio.Infrastructure.Configuracion;

// Configuración de los avisos (sección "Notificaciones").
// Las credenciales NO van en appsettings.json: se guardan con User Secrets
// (en desarrollo) para que nunca lleguen a GitHub.
public class OpcionesNotificaciones
{
    public OpcionesTelegram Telegram { get; set; } = new();
    public OpcionesCorreo Correo { get; set; } = new();

    // Minutos sin que nadie presione "Lo estoy atendiendo" antes de escalar.
    public int MinutosParaEscalar { get; set; } = 15;

    // Dirección del sistema, para incluir un enlace en los mensajes.
    public string? UrlSistema { get; set; }
}

public class OpcionesTelegram
{
    // Token que entrega @BotFather al crear el bot.
    public string? Token { get; set; }

    // Nombre de usuario del bot, sin "@" (por ejemplo, "CadenaFrioPostaBot").
    public string? NombreBot { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(NombreBot);
}

public class OpcionesCorreo
{
    public string Servidor { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;
    public string? Usuario { get; set; }

    // En Gmail: una "contraseña de aplicación", no la contraseña de la cuenta.
    public string? Contrasena { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Contrasena);
}
