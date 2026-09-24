# ***Diagrama de Agente para controlar servidor windows atravez de una apliacion movil***

```mermaid
flowchart TD
    App["App Expo (celular)"]

    subgraph host["Windows Server — host"]
        Agente["Agente .NET<br/>bind 127.0.0.1:5199"]
        Bitacora[("SQLite<br/>bitácora")]

        subgraph wsl["WSL2"]
            Docker["Docker Compose<br/>GestorTareas"]
        end
    end

    App -->|"HTTPS vía Tailscale<br/>X-Agente-Token"| Agente
    Agente --> Bitacora
    Agente -->|"Start-ScheduledTask"| wsl
    Agente -->|"docker ps / compose"| Docker
```

### Un pequeño agente para controlar wsl y servicioes dentro de este u afuera para mantenimiento y control del servidor via remoto usando tailscale sin necesidad de ssh.