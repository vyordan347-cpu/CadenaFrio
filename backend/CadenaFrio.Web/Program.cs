using CadenaFrio.Shared.Contracts;
using CadenaFrio.Web;
using CadenaFrio.Web.Autenticacion;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient apuntando al mismo servidor que entrega la app (la API).
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Las mismas políticas que la API, para decidir qué mostrar en pantalla.
// La seguridad real está en la API: la web solo oculta lo que el usuario no puede usar.
builder.Services.AddAuthorizationCore(opciones =>
{
    opciones.AddPolicy(Politicas.PuedeConfigurarRangos, politica =>
        politica.RequireRole(Roles.ResponsableInmunizaciones, Roles.Administrador));
});

// Hace disponible el estado de la sesión en todos los componentes.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ProveedorEstadoAutenticacion>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ProveedorEstadoAutenticacion>());

await builder.Build().RunAsync();
