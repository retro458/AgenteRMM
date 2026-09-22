using System.ComponentModel.DataAnnotations;

namespace AgenteServidor.Configuracion;

public class OpcionesSeguridad
{
    public const string Seccion = "Seguridad";
    

    [Required(AllowEmptyStrings = false, ErrorMessage = "Seguridad: El token es obligatorio.")]
    [MinLength(24, ErrorMessage = "Seguridad: El token debe tener al menos 24 caracteres.")]
    public string Token { get; set; } = "";
}
