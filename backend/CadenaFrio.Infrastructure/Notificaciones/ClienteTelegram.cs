using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CadenaFrio.Infrastructure.Configuracion;
using Microsoft.Extensions.Options;

namespace CadenaFrio.Infrastructure.Notificaciones;

// Cliente mínimo de la API de bots de Telegram (https://core.telegram.org/bots/api).
// Solo usa HTTP y JSON: no necesita librerías externas.
public class ClienteTelegram
{
    private readonly HttpClient _http;
    private readonly OpcionesTelegram _opciones;

    public ClienteTelegram(HttpClient http, IOptions<OpcionesNotificaciones> opciones)
    {
        _http = http;
        _opciones = opciones.Value.Telegram;
    }

    public bool Configurado => _opciones.Configurado;

    private string Url(string metodo) => $"https://api.telegram.org/bot{_opciones.Token}/{metodo}";

    // Envía un mensaje con formato HTML y, opcionalmente, un botón debajo.
    public async Task EnviarAsync(long chatId, string textoHtml, string? textoBoton = null, string? datoBoton = null,
        CancellationToken cancelacion = default)
    {
        var cuerpo = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["text"] = textoHtml,
            ["parse_mode"] = "HTML"
        };

        if (textoBoton is not null && datoBoton is not null)
        {
            // "inline_keyboard": botones dentro del mensaje. Al presionarlo, el bot recibe datoBoton.
            cuerpo["reply_markup"] = new
            {
                inline_keyboard = new[] { new[] { new { text = textoBoton, callback_data = datoBoton } } }
            };
        }

        var respuesta = await _http.PostAsJsonAsync(Url("sendMessage"), cuerpo, cancelacion);
        respuesta.EnsureSuccessStatusCode();
    }

    // Reemplaza el texto de un mensaje ya enviado (y quita su botón).
    public Task EditarAsync(long chatId, long mensajeId, string textoHtml, CancellationToken cancelacion = default) =>
        _http.PostAsJsonAsync(Url("editMessageText"),
            new { chat_id = chatId, message_id = mensajeId, text = textoHtml, parse_mode = "HTML" }, cancelacion);

    // Respuesta breve que Telegram muestra al presionar un botón.
    public Task ResponderBotonAsync(string idConsulta, string texto, CancellationToken cancelacion = default) =>
        _http.PostAsJsonAsync(Url("answerCallbackQuery"),
            new { callback_query_id = idConsulta, text = texto }, cancelacion);

    // Long polling: espera hasta 25 segundos a que llegue algo nuevo al bot.
    // Funciona aunque el servidor no tenga una dirección pública (como en la feria).
    public async Task<List<ActualizacionTelegram>> ObtenerActualizacionesAsync(long desde, CancellationToken cancelacion)
    {
        var respuesta = await _http.GetFromJsonAsync<RespuestaTelegram<List<ActualizacionTelegram>>>(
            Url($"getUpdates?offset={desde}&timeout=25&allowed_updates=[\"message\",\"callback_query\"]"), cancelacion);
        return respuesta?.Result ?? new();
    }
}

// ---------- Estructuras de la API de Telegram (solo los campos que se usan) ----------

public class RespuestaTelegram<T>
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("result")] public T? Result { get; set; }
}

public class ActualizacionTelegram
{
    [JsonPropertyName("update_id")] public long Id { get; set; }
    [JsonPropertyName("message")] public MensajeTelegram? Mensaje { get; set; }
    [JsonPropertyName("callback_query")] public BotonTelegram? Boton { get; set; }
}

public class MensajeTelegram
{
    [JsonPropertyName("message_id")] public long Id { get; set; }
    [JsonPropertyName("chat")] public ChatTelegram Chat { get; set; } = new();
    [JsonPropertyName("text")] public string? Texto { get; set; }
}

public class ChatTelegram
{
    [JsonPropertyName("id")] public long Id { get; set; }
}

public class BotonTelegram
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("data")] public string? Dato { get; set; }
    [JsonPropertyName("message")] public MensajeTelegram? Mensaje { get; set; }
}
