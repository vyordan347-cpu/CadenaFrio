using CadenaFrio.Domain.Entities;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CadenaFrio.Api.Inicializacion;

// Crea un usuario de prueba por cada rol, solo en desarrollo y solo si no hay usuarios.
// Sirve para probar y demostrar los permisos sin tener que registrar a nadie.
public static class SembradorUsuarios
{
    public static async Task SembrarAsync(IServiceProvider servicios)
    {
        using var alcance = servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CadenaFrioDbContext>();
        var cifrador = alcance.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();

        if (await db.Usuarios.AnyAsync())
        {
            return;
        }

        var usuarios = new[]
        {
            Crear("enfermera", "Enfermera de prueba", RolUsuario.Enfermera, "Enfermera123!"),
            Crear("responsable", "Responsable de prueba", RolUsuario.ResponsableInmunizaciones, "Responsable123!"),
            Crear("admin", "Administrador de prueba", RolUsuario.Administrador, "Admin123!")
        };

        db.Usuarios.AddRange(usuarios);
        await db.SaveChangesAsync();

        Usuario Crear(string nombreUsuario, string nombreCompleto, RolUsuario rol, string contrasena)
        {
            var usuario = new Usuario
            {
                NombreUsuario = nombreUsuario,
                NombreCompleto = nombreCompleto,
                Rol = rol
            };
            usuario.ContrasenaHash = cifrador.HashPassword(usuario, contrasena);
            return usuario;
        }
    }
}
