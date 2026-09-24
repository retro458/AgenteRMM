using AgenteServidor.Configuracion;
using Microsoft.Extensions.Options;
using AgenteServidor.Servicios;
using System.Text.Json.Serialization;
using AgenteServidor.Modelos;


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

builder.Services.ConfigureHttpJsonOptions(o => 
  o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

  builder.Services.AddSingleton<EjecutorComandos>();
// ---------------------------------------------------------------------
//constructor de la app.
var app = builder.Build();
// la tabla se crea antes de antender el primer request, para que no haya problemas de concurrencia.
await app.Services.GetRequiredService<BitacoraServicio>().InicializarAsync();
// RUTAS -----------------------------------------------------------------
app.MapGet("/config", (IOptions<OpcionesSeguridad> seguridad) => Results.Ok(new
{
    tokenCargado = !string.IsNullOrEmpty(seguridad.Value.Token),
    largo = seguridad.Value.Token.Length,
    prefijo = seguridad.Value.Token.Length >= 4 ? seguridad.Value.Token[..4] + "...." : "(vacio)"
}));

app.MapGet("/bitacora", async (
    BitacoraServicio bitacora,
    int? limite,
    TipoEvento? tipo,
    CancellationToken ct) =>
    Results.Ok(await bitacora.ConsultarAsync(limite ?? 50, tipo, null, ct)));


    // Temporal solo es para pruebas
    app.MapPost("/bitacora/prueba", async(BitacoraServicio bitacora, int? limite, TipoEvento? tipo, CancellationToken ct) =>
    {
        await bitacora.RegistrarAsync(
            TipoEvento.AccionManual, "prueba local", exito: true, accion: "Reiniciar wsl", detalle: "detalle de prueba", duracionMs: 123, ct: ct);
        return Results.Ok(new {mensaje = "Evento registrado correctamente"});
    });

    app.MapGet("/bitacora/resumen", async (BitacoraServicio bitacora, CancellationToken ct) =>
    Results.Ok(await bitacora.ResumenAsync(ct)));

    app.MapGet("/diagnostico/ejecutor", async(EjecutorComandos ejecutor, CancellationToken ct) =>
    {
       var esWindows = OperatingSystem.IsWindows();

       var exitoso = esWindows
       ? await ejecutor.EjecutarAsync("cmd.exe", "/c echo hola", TimeSpan.FromSeconds(5), ct:ct) 
       : await ejecutor.EjecutarAsync("/usr/bin/find", "hola", TimeSpan.FromSeconds(5), ct: ct);

       var Fallido = esWindows
       ? await ejecutor.EjecutarAsync("cmd.exe", "/c dir C:\\no-existe", TimeSpan.FromSeconds(5), ct:ct) 
       : await ejecutor.EjecutarAsync("/bin/ls", "/no-existe", TimeSpan.FromSeconds(5), ct: ct);

       var lento = esWindows
       ? await ejecutor.EjecutarAsync("cmd.exe", "/c timeout /t 5 /nobreak", TimeSpan.FromSeconds(5), ct:ct) 
       : await ejecutor.EjecutarAsync("/bin/sleep", "5", TimeSpan.FromSeconds(5), ct: ct);

       return Results.Ok(new
       {
           plataforma = esWindows ? "Windows" : "Linux",
           exitoso,
           Fallido,
           lento
       });
    });
// ---------------------------------------------------------------------

app.Run();