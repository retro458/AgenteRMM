namespace AgenteServidor.Modelos;

public record ResultadoComando(
    bool Exito,
    int CodigoSalida,
    string Salida,
    string Error,
    long DuracionMs,
    bool HuboTimeout
);