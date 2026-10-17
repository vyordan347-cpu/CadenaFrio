using CadenaFrio.Api.Inicializacion;
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

// Registro de lecturas sin duplicados (HU06) y motor de alertas (HU03).
builder.Services.AddScoped<RegistroLecturas>();
builder.Services.AddScoped<MotorAlertas>();

// Detector de silencio y su vigilante en segundo plano (HU04, HU05).
builder.Services.AddScoped<DetectorSilencio>();
builder.Services.AddHostedService<VigilanteSenal>();

// Canal de notificación de alertas. Para usar WhatsApp u otro canal,
// solo se reemplaza NotificadorRegistro por la nueva implementación.
builder.Services.AddScoped<INotificadorAlertas, NotificadorRegistro>();

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
        opciones.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Permisos por rol.
builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy(Politicas.PuedeConfigurarRangos, politica =>
        politica.RequireRole(Roles.ResponsableInmunizaciones, Roles.Administrador));
    opciones.AddPolicy(Politicas.EsAdministrador, politica =>
        politica.RequireRole(Roles.Administrador));
});

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

// Sirve los archivos de la app Blazor (CadenaFrio.Web) desde la misma API.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// El orden importa: primero se identifica al usuario, luego se revisan sus permisos.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Cualquier ruta que no sea de la API se entrega a Blazor,
// que decide qué pantalla mostrar.
app.MapFallbackToFile("index.html");

app.Run();
