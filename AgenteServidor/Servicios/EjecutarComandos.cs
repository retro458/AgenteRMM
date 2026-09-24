using System.Diagnostics;
using System.Text;
using AgenteServidor.Modelos;

namespace AgenteServidor.Servicios;

public class EjecutorComandos
{
    private readonly ILogger<EjecutorComandos> _log;

    public EjecutorComandos(ILogger<EjecutorComandos> log) => _log = log;

    public async Task<ResultadoComando> EjecutarAsync(
        string ejecutable,
        string argumentos,
        TimeSpan timeout,
        bool salidautf16 = false,
        CancellationToken ct = default)
    {
        var cronometro = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName = ejecutable,
            Arguments = argumentos,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, // necesario para redirigir la salida
            CreateNoWindow = true
        };

        // wsl.exe no soporta UTF-8, por lo que se fuerza a UTF-16
        if (salidautf16)
        {
            psi.StandardOutputEncoding = Encoding.Unicode;
            psi.StandardErrorEncoding = Encoding.Unicode;
        }

        try
        {
            using var proceso = new Process { StartInfo = psi};

            var salida = new StringBuilder();
            var error = new StringBuilder();

            proceso.OutputDataReceived += (_,e) => { if (e.Data is not null) salida.AppendLine(e.Data); };
            proceso.ErrorDataReceived += (_,e) => { if (e.Data is not null) error.AppendLine(e.Data); };

            proceso.Start();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                await proceso.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                MatarProceso(proceso);
                cronometro.Stop();

                _log.LogWarning("Timeout de {Seg}s ejecutando {Exe} {args}", timeout.TotalSeconds, ejecutable, argumentos);

                return new ResultadoComando(
                
                    Exito: false,
                    CodigoSalida: -1,
                    Salida: salida.ToString().Trim(),
                    Error: $"Timeout tras {timeout.TotalSeconds:NO} segundos",
                    DuracionMs:  cronometro.ElapsedMilliseconds,
                    HuboTimeout: true);
            }

            cronometro.Stop();

            return new ResultadoComando(
                Exito: proceso.ExitCode == 0,
                CodigoSalida: proceso.ExitCode,
                Salida: salida.ToString().Trim(),
                Error: error.ToString().Trim(),
                DuracionMs: cronometro.ElapsedMilliseconds,
                HuboTimeout: false);
        }
            catch (Exception ex)
            {

                cronometro.Stop();
                _log.LogError(ex, "Error ejecutando {Exe}", ejecutable);

                return new ResultadoComando(false, -1, "", ex.Message,
                    cronometro.ElapsedMilliseconds,false);
            }
        }
    
    private static void MatarProceso(Process p)
    {
        //entireProcess: wsl lanza hijos mantando al padre
        // quedan hueranos corriendo sin sentido

        try {if (!p.HasExited) p.Kill(entireProcessTree: true);}
        catch
        {
            /*puedo terminar entre el chequeo y el kil*/
        }
    }
}