using CadenaFrio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
 
var builder = WebApplication.CreateBuilder(args);
 
// Registra el DbContext usando la cadena de conexión de appsettings.json.
builder.Services.AddDbContext<CadenaFrioDbContext>(opciones =>
    opciones.UseSqlite(builder.Configuration.GetConnectionString("CadenaFrio")));
 
builder.Services.AddControllers();
builder.Services.AddOpenApi();
 
var app = builder.Build();
 
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Permite depurar la app Blazor desde el navegador.
    app.UseWebAssemblyDebugging();
}
 
if (!app.Environment.IsDevelopment())
{
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
 