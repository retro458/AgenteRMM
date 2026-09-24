namespace AgenteServidor.Modelos;

public enum TipoEvento
{
    AccionManual, // se preciono un boton en la interfaz web u movil.
    CaidaDetectada, // el watchdog detecto que el proceso principal se cayo y se reinicio.
    Recuperacion, // volvio a estar disponible el proceso principal despues de una caida.
    AutoReparacion, // el watchdog detecto que el proceso principal estaba caido y lo reinicio.
    
}

public record EntradaBitacora
(
    long Id,
    DateTime Momento,
    TipoEvento Tipo,
    string? Accion,
    string Origen,
    bool Exito,
    string? Detalle,
    long? DuracionMs
);

public record ConteoPorClave(string clave, int cantidad);

public record ResumenBitacora(
    int TotalEventos,
    List<ConteoPorClave> PorTipo,
    List<ConteoPorClave> CaidasPorMes,
    List<ConteoPorClave> CaidasPorHora
);