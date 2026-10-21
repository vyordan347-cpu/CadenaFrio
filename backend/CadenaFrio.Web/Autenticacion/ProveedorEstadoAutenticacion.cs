using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace CadenaFrio.Web.Autenticacion;

// Le dice a Blazor quién tiene la sesión iniciada, consultando a la API.
// La cookie de sesión la maneja el navegador: este código nunca la ve.
public class ProveedorEstadoAutenticacion : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly HttpClient _http;

    public ProveedorEstadoAutenticacion(HttpClient http)
    {
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var respuesta = await _http.GetAsync("api/auth/yo");
            if (!respuesta.IsSuccessStatusCode)
            {
                return Anonimo;
            }

            var usuario = await respuesta.Content.ReadFromJsonAsync<UsuarioActualDto>();
            if (usuario is null)
            {
                return Anonimo;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, usuario.NombreUsuario),
                new(ClaimTiposCadenaFrio.NombreCompleto, usuario.NombreCompleto),
                new(ClaimTypes.Role, usuario.Rol)
            };

            if (usuario.DebeCambiarContrasena)
            {
                claims.Add(new Claim(ClaimTiposCadenaFrio.DebeCambiarContrasena, "true"));
            }

            var identidad = new ClaimsIdentity(claims, authenticationType: "Cookie");

            return new AuthenticationState(new ClaimsPrincipal(identidad));
        }
        catch (Exception)
        {
            return Anonimo;
        }
    }

    // Devuelve null si el inicio de sesión fue correcto, o el mensaje de error.
    public async Task<string?> IniciarSesionAsync(LoginRequest solicitud)
    {
        try
        {
            var respuesta = await _http.PostAsJsonAsync("api/auth/login", solicitud);

            if (respuesta.IsSuccessStatusCode)
            {
                // Avisa a toda la app que la sesión cambió (menú, permisos, etc.).
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return null;
            }

            return await LeerMensajeAsync(respuesta) ?? "No se pudo iniciar sesión.";
        }
        catch (Exception)
        {
            return "No se pudo conectar con el servidor.";
        }
    }

    public async Task CerrarSesionAsync()
    {
        try
        {
            await _http.PostAsync("api/auth/logout", null);
        }
        finally
        {
            NotifyAuthenticationStateChanged(Task.FromResult(Anonimo));
        }
    }

    // Vuelve a consultar la sesión (por ejemplo, después de cambiar la contraseña) y espera
    // a tenerla: así, al navegar, la app ya no ve la marca de contraseña temporal.
    public async Task ActualizarEstadoAsync()
    {
        var estado = GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(estado);
        await estado;
    }

    // La API respondió 401: la sesión expiró o fue invalidada (usuario desactivado,
    // rol cambiado). Se avisa a la app, que lleva al usuario al inicio de sesión.
    public void SesionInvalidada() => NotifyAuthenticationStateChanged(Task.FromResult(Anonimo));

    private static async Task<string?> LeerMensajeAsync(HttpResponseMessage respuesta)
    {
        try
        {
            using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            return documento.RootElement.TryGetProperty("mensaje", out var mensaje) ? mensaje.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
