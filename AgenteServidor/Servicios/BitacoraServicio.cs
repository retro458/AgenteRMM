using AgenteServidor.Configuracion;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using System.Globalization;
using AgenteServidor.Modelos;
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

    // Metodo para inicializar la base de datos y crear la tabla si no existe, y eliminar registros antiguos segun la politica de retencion.
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
                Tipo TEXT NOT NULL
                      CHECK (Tipo IN ('AccionManual', 'CaidaDetectada', 'Recuperacion', 'AutoReparacion')),
                Accion TEXT,
                Origen TEXT NOT NULL,
                Exito INTEGER NOT NULL CHECK (Exito IN (0, 1)),
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
      //MEetodo para ejecutar un comando sql en la base de datos, usado para inicializar la tabla y los indices.
        private static async Task Ejecutar(SqliteConnection cn, string sql, CancellationToken ct = default)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    //Metodo para registrar un evento en la bitacora, con parametros opcionales de accion, detalle y duracion.
    public async Task RegistrarAsync(
        TipoEvento tipo,
        string origen,
        bool exito,
        string? accion = null,
        string? detalle = null,
        long? duracionMs = null,
        CancellationToken ct = default)
    {
        try {
        await using var cn = new SqliteConnection(_cadenaConexion);
        await cn.OpenAsync(ct);

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Bitacora (Momento, Tipo, Accion, Origen, Exito, Detalle, DuracionMs)
            VALUES ($momento, $tipo, $accion, $origen, $exito, $detalle, $duracionMs);
        """;
        cmd.Parameters.AddWithValue("$momento", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$tipo",tipo.ToString());
        cmd.Parameters.AddWithValue("$accion", accion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$origen", origen);
        cmd.Parameters.AddWithValue("$exito", exito ? 1 : 0);
        cmd.Parameters.AddWithValue("$detalle", detalle ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$duracionMs", duracionMs ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // la bitacora no debe bloquear el funcionamiento del servicio, si hay un error al registrar un evento, se loguea y se sigue.
            _log.LogError(ex, "Bitacora: Error al registrar evento de bitacora");
        }
    }
    
    // Metodo para consultar la bitacora, con filtros opcionales de tipo y fecha desde.
    public async Task<List<EntradaBitacora>> ConsultarAsync(
        int limite = 50, 
        TipoEvento? tipo = null,
        DateTime? desde = null,
        CancellationToken ct = default)
    {
        var lista = new List<EntradaBitacora>();

        await using var cn = new SqliteConnection(_cadenaConexion);
        await cn.OpenAsync(ct);

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Momento, Tipo, Accion, Origen, Exito, Detalle, DuracionMs
            FROM Bitacora
            WHERE ($tipo IS NULL OR Tipo = $tipo)
              AND ($desde IS NULL OR Momento >= $desde)
            ORDER BY Momento DESC
            LIMIT $limite;
        """;
        cmd.Parameters.AddWithValue("$tipo", tipo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$limite", limite);
        cmd.Parameters.AddWithValue("$desde", desde ?? (object)DBNull.Value);

        await using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            lista.Add(new EntradaBitacora(
                Id: lector.GetInt64(0),
                Momento: DateTime.Parse(lector.GetString(1), null, DateTimeStyles.RoundtripKind),
                Tipo: Enum.Parse<TipoEvento>(lector.GetString(2)),
                Accion: lector.IsDBNull(3) ? null : lector.GetString(3),
                Origen: lector.GetString(4),
                Exito: lector.GetInt32(5) == 1,
                Detalle: lector.IsDBNull(6) ? null : lector.GetString(6),
                DuracionMs: lector.IsDBNull(7) ? null : lector.GetInt64(7)
            ));
        }

        return lista;
    }

    //MWtodo par ver el resumen
    public async Task<ResumenBitacora> ResumenAsync(CancellationToken ct = default)
    {
        await using var cn = new SqliteConnection(_cadenaConexion);
        await cn.OpenAsync(ct);
        
        await using var cmdTotal = cn.CreateCommand();
        cmdTotal.CommandText = "SELECT COUNT(*) FROM Bitacora;";
        var total = Convert.ToInt32(await cmdTotal.ExecuteScalarAsync(ct));
        
        var PorTipo = await ConteosAsync(cn, """
            SELECT Tipo, count(*) FROM Bitacora 
            GROUP BY Tipo ORDER BY COUNT(*) DESC;
            """, ct);

        var PorMes = await ConteosAsync(cn, """
            SELECT strftime('%Y-%m', Momento, 'localtime') as Mes, count(*) FROM Bitacora 
            WHERE Tipo = 'CaidaDetectada'
            GROUP BY Mes ORDER BY Mes DESC LIMIT 12;
            """, ct);

        var PorHora = await ConteosAsync(cn, """
            SELECT strftime('%H', Momento, 'localtime') as hora, count(*) FROM Bitacora 
            WHERE Tipo = 'CaidaDetectada'
            GROUP BY Hora ORDER BY COUNT(*) DESC;
            """, ct);
        return new ResumenBitacora(total, PorTipo, PorMes, PorHora);
    }

    private static async Task<List<ConteoPorClave>> ConteosAsync(SqliteConnection cn, string sql, CancellationToken ct)
    {
        var lista = new List<ConteoPorClave>();

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;

        await using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            lista.Add(new ConteoPorClave(
                lector.GetString(0),
                lector.GetInt32(1)));
        }
        return lista;
    }
    

}
