using AgenteServidor.Configuracion;
using Microsoft.Extensions.Options;
using AgenteServidor.Servicios;


var rutaEnv = CargadorDotEnv.Cargar();

var builder = WebApplication.CreateBuilder(args);

// SERVICIOS PROPIOS --------------------------------------------------
builder.Services.AddOptions<OpcionesSeguridad>()
    .Bind(builder.Configuration.GetSection(OpcionesSeguridad.Seccion))
    .ValidateDataAnnotations()
    .ValidateOnStart();;

builder.Services.AddOptions<OpcionesBitacora>()
    .Bind(builder.Configuration.GetSection(OpcionesBitacora.Seccion))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<BitacoraServicio>();
// ---------------------------------------------------------------------
//constructor de la app.
var app = builder.Build();
// la tabla se crea antes de antender el primer request, para que no haya problemas de concurrencia.
await app.Services.GetRequiredService<BitacoraServicio>().InicializarAsync();

app.MapGet("/config", (IOptions<OpcionesSeguridad> seguridad) => Results.Ok(new
{
    tokenCargado = !string.IsNullOrEmpty(seguridad.Value.Token),
    largo = seguridad.Value.Token.Length,
    prefijo = seguridad.Value.Token.Length >= 4 ? seguridad.Value.Token[..4] + "...." : "(vacio)"
}));

app.Run();