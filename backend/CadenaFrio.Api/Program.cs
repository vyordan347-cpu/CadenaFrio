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
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
