using System.Security.Claims;
using CadenaFrio.Domain.Enums;
using CadenaFrio.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CadenaFrio.Tests;

// Matriz de permisos según la NTS 242: se prueba con las mismas políticas que usan la API y la web
// (Politicas.Configurar). El administrador puede hacer todo.
public class AutorizacionTests
{
    private static readonly IAuthorizationService Autorizacion = CrearServicio();

    private static IAuthorizationService CrearServicio()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAuthorizationCore(Politicas.Configurar);
        return servicios.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    // Usuario con sesión iniciada y el rol indicado (como lo arma SesionUsuario).
    private static ClaimsPrincipal Usuario(string rol) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "prueba"), new Claim(ClaimTypes.Role, rol) }, "Prueba"));

    private static async Task<bool> PuedeAsync(string rol, string politica) =>
        (await Autorizacion.AuthorizeAsync(Usuario(rol), politica)).Succeeded;

    [Theory]
    // Rangos de conservación: responsable de inmunizaciones (5.1.43) y administrador.
    [InlineData(Roles.Enfermera, Politicas.PuedeConfigurarRangos, false)]
    [InlineData(Roles.ResponsableInmunizaciones, Politicas.PuedeConfigurarRangos, true)]
    [InlineData(Roles.MedicoJefe, Politicas.PuedeConfigurarRangos, false)]
    [InlineData(Roles.Administrador, Politicas.PuedeConfigurarRangos, true)]
    // Revisión de rupturas (Anexo 13): el jefe a través del responsable (6.10.2), y el administrador.
    [InlineData(Roles.Enfermera, Politicas.PuedeRevisarRupturas, false)]
    [InlineData(Roles.ResponsableInmunizaciones, Politicas.PuedeRevisarRupturas, true)]
    [InlineData(Roles.MedicoJefe, Politicas.PuedeRevisarRupturas, true)]
    [InlineData(Roles.Administrador, Politicas.PuedeRevisarRupturas, true)]
    // Administración del sistema: solo el administrador.
    [InlineData(Roles.Enfermera, Politicas.EsAdministrador, false)]
    [InlineData(Roles.ResponsableInmunizaciones, Politicas.EsAdministrador, false)]
    [InlineData(Roles.MedicoJefe, Politicas.EsAdministrador, false)]
    [InlineData(Roles.Administrador, Politicas.EsAdministrador, true)]
    public async Task Cada_rol_tiene_los_permisos_de_la_matriz(string rol, string politica, bool permitido)
    {
        Assert.Equal(permitido, await PuedeAsync(rol, politica));
    }

    [Fact]
    public async Task El_administrador_puede_todo()
    {
        foreach (var politica in Politicas.RolesPermitidos.Keys)
        {
            Assert.True(await PuedeAsync(Roles.Administrador, politica), politica);
        }
    }

    [Fact]
    public async Task Un_rol_desconocido_no_tiene_ningun_permiso()
    {
        foreach (var politica in Politicas.RolesPermitidos.Keys)
        {
            Assert.False(await PuedeAsync("Visitante", politica), politica);
        }
    }

    [Fact]
    public void Los_roles_de_la_web_coinciden_con_los_del_dominio()
    {
        // El rol viaja en la sesión como el nombre del enum (RolUsuario.ToString()).
        Assert.Equal(Enum.GetNames<RolUsuario>().Order(), Roles.Todos.Order());
        Assert.Equal(4, (int)RolUsuario.MedicoJefe); // valor guardado en la base: no se cambia
    }

    [Fact]
    public void Cada_rol_tiene_un_nombre_visible_de_la_norma()
    {
        Assert.Equal("Técnico de enfermería", Roles.Nombre(Roles.Enfermera));
        Assert.Equal("Responsable de inmunizaciones", Roles.Nombre(Roles.ResponsableInmunizaciones));
        Assert.Equal("Médico jefe del establecimiento", Roles.Nombre(Roles.MedicoJefe));
        Assert.Equal("Administrador", Roles.Nombre(Roles.Administrador));
    }
}
