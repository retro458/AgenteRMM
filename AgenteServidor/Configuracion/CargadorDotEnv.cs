namespace AgenteServidor.Configuracion;

public static class CargadorDotEnv
{
   /// <summary>
   /// Carga las variables de entorno desde un archivo .env en el directorio raíz del proyecto,
   /// devuelve la ruta que uso, o null sin si no encontró el archivo.
   /// </summary>
    public static string? Cargar()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 5 && dir is not null; i++)
        {
            var archivo = Path.Combine(dir.FullName, ".env");
            if (File.Exists(archivo))
            {
                DotNetEnv.Env.Load(archivo);
                return archivo;
            }
            dir = dir.Parent;
        }
        return null;
    }
}