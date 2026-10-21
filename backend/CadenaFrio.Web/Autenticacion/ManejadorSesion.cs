using System.Net;

namespace CadenaFrio.Web.Autenticacion;

// Revisa cada respuesta de la API. Si es 401 (sesión vencida o invalidada), avisa a la app
// para que lleve al usuario al inicio de sesión, en lugar de mostrar errores sueltos.
public class ManejadorSesion : DelegatingHandler
{
    private readonly IServiceProvider _servicios;

    // Se recibe el proveedor de servicios y no el estado de autenticación directamente,
    // porque este también usa el HttpClient: así se evita una dependencia circular.
    public ManejadorSesion(IServiceProvider servicios)
    {
        _servicios = servicios;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        var respuesta = await base.SendAsync(solicitud, cancelacion);

        // Las rutas de sesión (/api/auth) manejan su propio 401: por ejemplo, "contraseña incorrecta".
        var esRutaDeSesion = solicitud.RequestUri?.AbsolutePath.StartsWith("/api/auth") == true;

        if (respuesta.StatusCode == HttpStatusCode.Unauthorized && !esRutaDeSesion)
        {
            _servicios.GetRequiredService<ProveedorEstadoAutenticacion>().SesionInvalidada();
        }

        return respuesta;
    }
}
