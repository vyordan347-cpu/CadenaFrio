using System.Net;
using System.Net.Mail;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Copia por correo de las alertas urgentes: respaldo y registro formal.
public class NotificadorCorreo : INotificadorAlertas
{
    private readonly CadenaFrioDbContext _db;
    private readonly OpcionesCorreo _opciones;

    public NotificadorCorreo(CadenaFrioDbContext db, IOptions<OpcionesNotificaciones> opciones)
    {
        _db = db;
        _opciones = opciones.Value.Correo;
    }

    public async Task AlertaAbiertaAsync(Alerta alerta, string nombreEquipo)
    {
        if (MensajesAlerta.EsUrgente(alerta))
        {
            var (titulo, cuerpo) = MensajesAlerta.Apertura(alerta, nombreEquipo);
            await EnviarAsync(await DestinatariosAsync(soloEscalamiento: false), titulo, cuerpo);
        }
    }

    public async Task AlertaEscaladaAsync(Alerta alerta, string nombreEquipo)
    {
        var (titulo, cuerpo) = MensajesAlerta.Apertura(alerta, nombreEquipo);
        await EnviarAsync(await DestinatariosAsync(soloEscalamiento: false), titulo, cuerpo);
    }

    // El cierre no se envía por correo: el correo es para lo que requiere acción.
    public Task AlertaCerradaAsync(Alerta alerta, string nombreEquipo) => Task.CompletedTask;

    // También lo usa el escalamiento, con sus propios destinatarios.
    public async Task EnviarAsync(List<string> destinatarios, string titulo, string cuerpoHtml)
    {
        if (!_opciones.Configurado || destinatarios.Count == 0)
        {
            return;
        }

        using var cliente = new SmtpClient(_opciones.Servidor, _opciones.Puerto)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Contrasena)
        };

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_opciones.Usuario!, "Cadena de Frío"),
            Subject = MensajesAlerta.TextoPlano(titulo),
            Body = MensajesAlerta.TextoPlano(cuerpoHtml) + "\n\n— Sistema de monitoreo de cadena de frío"
        };

        foreach (var destino in destinatarios)
        {
            mensaje.To.Add(destino);
        }

        await cliente.SendMailAsync(mensaje);
    }

    public Task<List<string>> DestinatariosAsync(bool soloEscalamiento) =>
        _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo && u.Correo != null && u.Correo != ""
                        && (soloEscalamiento ? u.EsContactoEscalamiento : u.RecibeAlertas))
            .Select(u => u.Correo!)
            .ToListAsync();
}
