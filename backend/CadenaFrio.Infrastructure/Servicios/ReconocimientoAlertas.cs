using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Infrastructure.Servicios;

public record ResultadoReconocimiento(bool Exito, string Mensaje, string? NombreEquipo = null);

// "Lo estoy atendiendo": lo usan el botón de Telegram y el de la web.
public class ReconocimientoAlertas
{
    private readonly CadenaFrioDbContext _db;
    private readonly ClienteTelegram _telegram;
    private readonly Bitacora _bitacora;

    public ReconocimientoAlertas(CadenaFrioDbContext db, ClienteTelegram telegram, Bitacora bitacora)
    {
        _db = db;
        _telegram = telegram;
        _bitacora = bitacora;
    }

    public async Task<ResultadoReconocimiento> ReconocerAsync(int alertaId, string usuario, long? chatQueReconoce = null)
    {
        var alerta = await _db.Alertas.Include(a => a.Equipo).FirstOrDefaultAsync(a => a.Id == alertaId);
        if (alerta is null)
        {
            return new(false, "La alerta no existe.");
        }

        if (alerta.ReconocidaPor is not null)
        {
            return new(false, $"Ya la está atendiendo {alerta.ReconocidaPor}.", alerta.Equipo.Nombre);
        }

        alerta.ReconocidaPor = usuario;
        alerta.FechaReconocimiento = DateTime.UtcNow;
        _bitacora.Registrar(CategoriaAuditoria.Alertas, "Marcó \"Lo estoy atendiendo\"", usuario,
            $"{alerta.Equipo.Nombre}, alerta {alerta.Id}" + (chatQueReconoce is null ? " (desde la web)." : " (desde Telegram)."));
        await _db.SaveChangesAsync();

        // Se avisa a los demás, para que nadie duplique esfuerzos.
        if (_telegram.Configurado)
        {
            var otros = await _db.Usuarios
                .AsNoTracking()
                .Where(u => u.Activo && (u.RecibeAlertas || u.EsContactoEscalamiento)
                            && u.TelegramChatId != null && u.TelegramChatId != chatQueReconoce)
                .Select(u => u.TelegramChatId!.Value)
                .ToListAsync();

            foreach (var chat in otros)
            {
                try
                {
                    await _telegram.EnviarAsync(chat,
                        $"👤 <b>{System.Net.WebUtility.HtmlEncode(usuario)}</b> está atendiendo la alerta de <b>{System.Net.WebUtility.HtmlEncode(alerta.Equipo.Nombre)}</b>.");
                }
                catch (HttpRequestException)
                {
                    // Un aviso informativo que falla no impide registrar la atención.
                }
            }
        }

        return new(true, "Registrado: usted está atendiendo la alerta.", alerta.Equipo.Nombre);
    }
}
