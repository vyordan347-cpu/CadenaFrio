using CadenaFrio.Domain.Servicios;
using CadenaFrio.Infrastructure.Data;
using CadenaFrio.Infrastructure.Notificaciones;
using CadenaFrio.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registra el DbContext usando la cadena de conexión de appsettings.json.
builder.Services.AddDbContext<CadenaFrioDbContext>(opciones =>
    opciones.UseSqlite(builder.Configuration.GetConnectionString("CadenaFrio")));

// Motor de alertas (HU03).
builder.Services.AddScoped<MotorAlertas>();

// Canal de notificación de alertas. Para usar WhatsApp u otro canal,
// solo se reemplaza NotificadorRegistro por la nueva implementación.
builder.Services.AddScoped<INotificadorAlertas, NotificadorRegistro>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Permite depurar la app Blazor desde el navegador.
    app.UseWebAssemblyDebugging();
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

app.UseAuthorization();
app.MapControllers();

// Cualquier ruta que no sea de la API se entrega a Blazor,
// que decide qué pantalla mostrar.
app.MapFallbackToFile("index.html");

app.Run();
