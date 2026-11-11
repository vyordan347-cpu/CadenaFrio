using CadenaFrio.Api.Inicializacion;
using CadenaFrio.Api.Seguridad;
using CadenaFrio.Api.Vigilancia;
using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Configuracion;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Notificaciones;
using CadenaFrio.Infrastructure.Servicios;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registra el DbContext usando la cadena de conexión de appsettings.json.
builder.Services.AddDbContext<CadenaFrioDbContext>(opciones =>
    opciones.UseSqlite(builder.Configuration.GetConnectionString("CadenaFrio")));

// Tiempos del monitoreo, desde la sección "Monitoreo" de appsettings.json.
builder.Services.Configure<OpcionesMonitoreo>(builder.Configuration.GetSection("Monitoreo"));

// Respaldos de la base (sección "Respaldos"). Una carpeta relativa se toma desde la carpeta
// de la API, no desde donde se ejecute "dotnet run".
builder.Services.Configure<OpcionesRespaldo>(builder.Configuration.GetSection("Respaldos"));
builder.Services.PostConfigure<OpcionesRespaldo>(o =>
    o.Carpeta = Path.GetFullPath(o.Carpeta, builder.Environment.ContentRootPath));

// Datos del establecimiento para el formato del Anexo N° 13 (sección "Establecimiento").
builder.Services.Configure<OpcionesEstablecimiento>(builder.Configuration.GetSection("Establecimiento"));

// Registro de lecturas sin duplicados (HU06) y motor de alertas (HU03).
builder.Services.AddScoped<RegistroLecturas>();
builder.Services.AddScoped<MotorAlertas>();

// Rangos calculados según las vacunas de cada equipo.
builder.Services.AddScoped<GestionRangos>();

// Bitácora de auditoría (Fase 5b): quién hizo qué y cuándo.
builder.Services.AddScoped<Bitacora>();
builder.Services.AddScoped<ConsultaBitacora>();

// Indicadores clave para Reportes y su Excel.
builder.Services.AddScoped<CalculoIndicadores>();

// Respaldos de la base (Fase 5c).
builder.Services.AddScoped<RespaldoBaseDatos>();
builder.Services.AddHostedService<RespaldoDiario>();

// Detector de silencio y su vigilante en segundo plano (HU04, HU05).
builder.Services.AddScoped<DetectorSilencio>();
builder.Services.AddHostedService<VigilanteSenal>();

// Avisos por varios canales (Fase 2): consola, Telegram y correo.
// Las credenciales se leen de User Secrets, nunca de archivos que suben a GitHub.
builder.Services.Configure<OpcionesNotificaciones>(builder.Configuration.GetSection("Notificaciones"));
builder.Services.AddHttpClient<ClienteTelegram>(cliente => cliente.Timeout = TimeSpan.FromSeconds(40));
builder.Services.AddScoped<NotificadorRegistro>();
builder.Services.AddScoped<NotificadorTelegram>();
builder.Services.AddScoped<NotificadorCorreo>();
builder.Services.AddScoped<INotificadorAlertas, NotificadorCompuesto>();
builder.Services.AddScoped<ReconocimientoAlertas>();
builder.Services.AddScoped<EscaladorAlertas>();
builder.Services.AddHostedService<BotTelegram>();

// Resumen del día anterior por Telegram, cada mañana (sección "ResumenDiario").
builder.Services.Configure<OpcionesResumenDiario>(builder.Configuration.GetSection("ResumenDiario"));
builder.Services.AddScoped<ResumenDiario>();
builder.Services.AddHostedService<EnvioResumenDiario>();

// Cifrado de contraseñas (RNF05).
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// Autenticación con cookie (RF03).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "CadenaFrio.Sesion";
        // HttpOnly: el código JavaScript del navegador no puede leer la cookie.
        opciones.Cookie.HttpOnly = true;
        // Strict: la cookie no se envía desde otros sitios (protege contra ataques CSRF).
        opciones.Cookie.SameSite = SameSiteMode.Strict;
        // La sesión dura 8 horas (un turno) y se renueva mientras el usuario esté activo.
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;

        // Por defecto, ASP.NET redirige a una página de login del servidor.
        // Como es una API, se responde 401 (sin sesión) o 403 (sin permiso).
        opciones.Events.OnRedirectToLogin = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        // En cada petición se verifica que el usuario siga activo y con el mismo sello.
        opciones.Events.OnValidatePrincipal = SesionUsuario.ValidarAsync;
        opciones.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Permisos por rol: la matriz está en Shared (Politicas.RolesPermitidos), la misma que usa la web.
builder.Services.AddAuthorization(Politicas.Configurar);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Permite depurar la app Blazor desde el navegador.
    app.UseWebAssemblyDebugging();

    // Usuarios de prueba, uno por rol.
    await SembradorUsuarios.SembrarAsync(app.Services);
}
else
{
    // En producción todo va por HTTPS (RNF05). En desarrollo se omite para
    // que el ESP32 pueda enviar lecturas por HTTP sin redirecciones.
    app.UseHttpsRedirection();
}

// El orden importa: primero se identifica al usuario, luego se revisan sus permisos.
app.UseAuthentication();

// Con contraseña temporal, solo se permiten las rutas de sesión hasta que la cambie.
app.Use(SesionUsuario.ExigirCambioDeContrasenaAsync);

app.UseAuthorization();

app.MapControllers();

// Archivos de la app Blazor (CadenaFrio.Web), incluidos los de _framework: CSS, JS, fuentes,
// imágenes y WebAssembly. Reemplaza a UseBlazorFrameworkFiles + UseStaticFiles (no se combinan).
// A diferencia de UseStaticFiles, entrega cada archivo
// también con una huella del contenido en el nombre (sistema.36x54xoqzg.css) y le indica al
// navegador cuánto guardarlo: así, después de un cambio, nunca se usa un CSS viejo.
app.MapStaticAssets();

// Cualquier ruta que no sea de la API se entrega a Blazor,
// que decide qué pantalla mostrar.
app.MapFallbackToFile("index.html");

app.Run();
