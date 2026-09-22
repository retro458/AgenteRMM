using AgenteServidor.Configuracion;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;

namespace AgenteServidor.Servicios;

public class BitacoraServicio
{
    private readonly OpcionesBitacora _opciones;
    private readonly ILogger<BitacoraServicio> _log;
    private readonly string _cadenaConexion;

    public BitacoraServicio(IOptions<OpcionesBitacora> opciones, ILogger<BitacoraServicio> log)
    {
        _opciones = opciones.Value;
        _log = log;

        var ruta = _opciones.RutaAbsoluta();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        _cadenaConexion = new SqliteConnectionStringBuilder
        {
            DataSource = ruta,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        _log.LogInformation("Bitacora: Inicializando base de datos en {ruta}", ruta);

    }

    public async Task InicializarAsync(CancellationToken ct = default)
    {
        await using var cn = new SqliteConnection(_cadenaConexion);
        await cn.OpenAsync(ct);

        //WAL permite leer y escribir al mismo tiempo, y es más rápido que el modo por defecto y 
        // el watchdog y una consulta sin bloqueos entre si.

        await Ejecutar(cn, "PRAGMA journal_mode=WAL;", ct);

        await Ejecutar(cn, """
            CREATE TABLE IF NOT EXISTS Bitacora (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Momento TEXT NOT NULL,
                Tipo INTEGER NOT NULL,
                Accion TEXT,
                Origen TEXT NOT NULL,
                Exito INTEGER NOT NULL,
                Detalle TEXT,
                DuracionMs INTEGER
            );
        """, ct);

        await Ejecutar(cn, """
            CREATE INDEX IF NOT EXISTS IX_Bitacora_Momento ON Bitacora (Momento DESC);
        """, ct);
        await Ejecutar(cn, """
            CREATE INDEX IF NOT EXISTS IX_Bitacora_Tipo ON Bitacora (Tipo, Momento DESC);
        """, ct);

        // retencion: corre al arrancar y no hace falta un job aparte para que este funcione.
        var corte = DateTime.UtcNow.AddDays(-_opciones.DiasRetencion).ToString("O"); // sirve para hacer la fecha del dia de corte legibre es decir con el formato ida y vuelta iso 8601 yyyy-MM-ddTHH:mm:ss.fffffffK
        await using var borrado = cn.CreateCommand();
        borrado.CommandText = "DELETE FROM Bitacora WHERE Momento < $corte";
        borrado.Parameters.AddWithValue("$corte", corte);
        var eliminadas = await borrado.ExecuteNonQueryAsync(ct);

        if (eliminadas > 0)
    
            _log.LogInformation("Bitacora: Se eliminaron {eliminadas} registros antiguos de la bitacora", eliminadas);
    
      }
        private static async Task Ejecutar(SqliteConnection cn, string sql, CancellationToken ct = default)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }

}
