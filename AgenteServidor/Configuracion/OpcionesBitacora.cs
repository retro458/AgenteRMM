using System.ComponentModel.DataAnnotations;

namespace AgenteServidor.Configuracion;

public class OpcionesBitacora
{
    public const string Seccion = "Bitacora";
    public string RutaArchivo { get; set; } = "datos/bitacora.db";
    
    [Range(7, 3650, ErrorMessage = "Bitacora: La retención de registros debe estar entre 7 y 3650 días.")]
    public int DiasRetencion { get; set; } = 90;

    /// <summary>
    /// si la ruta es relativa, se resuelve contra la carpeta del ejecutable,
    /// como servicio de win, el workin directory es C:\Windows\System32, por lo que conviene usar rutas absolutas o relativas a la carpeta del ejecutable.
    ///  para: mi
    ///  de: mi 
    /// fin :D
    /// </summary>
    public string RutaAbsoluta() =>
        Path.IsPathRooted(RutaArchivo) ? RutaArchivo : Path.Combine(AppContext.BaseDirectory, RutaArchivo);

}