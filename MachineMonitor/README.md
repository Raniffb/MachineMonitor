# MachineMonitor

Simulador SCADA industrial desenvolvido em **C# com Avalonia UI**, utilizando o protocolo **Modbus TCP** para comunicação com dispositivos de campo. O projeto simula o monitoramento e controle de uma máquina industrial em tempo real, com sistema de alarmes, log de eventos e controle de setpoints.

---

## Visão geral

O MachineMonitor possui dois modos de operação:

- **Modo Fake** — dados simulados internamente em C#, sem necessidade de rede ou servidor externo. Ideal para desenvolvimento e demonstração.
- **Modo Modbus TCP** — conecta a um servidor Modbus TCP real (`modbus_server.py`), que simula a dinâmica de uma máquina industrial com perturbações de processo aleatórias.

---

## Funcionalidades

### Dashboard
- Leitura em tempo real de **temperatura**, **pressão**, **velocidade do motor** e **contagem de produção**
- Cards de status da máquina (LIGADA / DESLIGADA) e de alarme (ATIVO / INATIVO)
- Exibição da causa do alarme com persistência de cache (evita perda de motivo em picos transientes)
- Indicador de atualização ao vivo com timestamp

### Controles do operador
- Ligar e desligar a máquina
- Resetar alarme manualmente
- Enviar setpoints de temperatura (°C) e velocidade do motor (RPM)

### Sistema de alarme
| Condição | Limiar | Causa |
|----------|--------|-------|
| Temperatura alta | > 95 °C | Superaquecimento |
| Pressão alta | > 7,0 bar | Sobrepressão |
| Pressão baixa | < 1,0 bar | Baixa pressão |
| Velocidade baixa (máquina ligada) | < 200 rpm | Falha de partida |

- Alarme é **latchado**: permanece ativo até reset manual do operador
- **Safe state**: ao alarmar, a máquina é desligada automaticamente
- **Janela de supressão** de 3 segundos após reset (evita redisparo imediato)
- Fluxo do operador: alarme dispara → máquina para → operador reseta → operador religa

### Log de eventos
Registra automaticamente: disparo de alarme, reset de alarme, máquina ligada/desligada, alteração de setpoints e erros de comunicação.

---

## Estrutura do projeto

```
MachineMonitor/
├── Models/
│   ├── MachineData.cs          — DTO com os dados lidos da máquina
│   └── ModbusAddressMap.cs     — mapa de endereços Modbus
├── Services/
│   ├── IModbusService.cs       — interface de comunicação
│   ├── FakeModbusService.cs    — implementação simulada (sem rede)
│   ├── NModbusService.cs       — implementação real via NModbus TCP
│   ├── ModbusServiceProxy.cs   — proxy singleton (seleciona Fake ou TCP)
│   └── LogService.cs           — serviço de log de eventos
├── ViewModels/
│   ├── DashboardViewModel.cs   — lógica do dashboard (poll loop, comandos)
│   └── ConnectViewModel.cs     — tela de conexão e seleção de modo
├── Views/
│   ├── DashboardView.axaml     — interface do dashboard
│   └── ConnectView.axaml       — interface de conexão
modbus_server.py                — servidor Modbus TCP (Python)
```

---

## Mapa de endereços Modbus

| Função Modbus | Endereço | Descrição | Escala |
|---------------|----------|-----------|--------|
| FC05 Coil 0 | `MachinePower` | Liga / desliga máquina | bool |
| FC05 Coil 1 | `ResetAlarm` | Pulso de reset de alarme | bool |
| FC02 DI 0 | `EmergencyActive` | Alarme ativo | bool |
| FC02 DI 1 | `MachineRunning` | Máquina em execução | bool |
| FC04 IR 0 | `Temperature` | Temperatura | °C × 10 |
| FC04 IR 1 | `Pressure` | Pressão | bar × 100 |
| FC04 IR 2 | `MotorSpeed` | Velocidade do motor | RPM |
| FC04 IR 3 | `ProductionCounter` | Contador de produção | int |
| FC06 HR 0 | `TemperatureSetpoint` | Setpoint de temperatura | °C × 10 |
| FC06 HR 1 | `MotorSpeedSetpoint` | Setpoint de velocidade | RPM |

---

## Requisitos

### C# / .NET
- .NET 8.0 ou superior
- Pacotes NuGet:
  - `Avalonia 11.2.3`
  - `Avalonia.Themes.Fluent 11.2.3`
  - `CommunityToolkit.Mvvm 8.4.0`
  - `NModbus 3.0.81`

### Python (servidor simulado)
- Python 3.10+
- `pip install "pymodbus==3.6.9"`

---

## Como executar

### Modo Fake (sem servidor)
```bash
dotnet run
```
Na tela de conexão, selecione **Modo Fake** e clique em conectar.

### Modo Modbus TCP (com servidor Python)

1. Inicie o servidor:
```bash
python modbus_server.py
```

2. Execute o projeto C#:
```bash
dotnet run
```

3. Na tela de conexão, selecione **Modo Modbus TCP** com as configurações:
   - Host: `127.0.0.1`
   - Porta: `5020`
   - Unit ID: `1`

---

## Servidor Python — detalhes

O `modbus_server.py` simula a dinâmica real de uma máquina industrial:

- **Oscilação normal**: temperatura ±5 °C em torno do setpoint, pressão entre 4,5–6,0 bar, motor ±100 rpm
- **Perturbações de processo**: 2% de chance por ciclo (≈ 1 evento a cada 50 s), sorteando aleatoriamente entre superaquecimento, sobrepressão, baixa pressão ou stall do motor
- **Safe state**: ao alarmar, o coil de energia da máquina é desligado automaticamente
- **Console**: exibe linha atualizada em tempo real com todos os valores e status de alarme

---

## Tecnologias utilizadas

| Tecnologia | Uso |
|------------|-----|
| C# / .NET 8 | Linguagem principal |
| Avalonia UI 11.2.3 | Interface gráfica multiplataforma |
| CommunityToolkit.Mvvm | MVVM com source generators |
| NModbus 3.0.81 | Cliente Modbus TCP |
| Python 3 + pymodbus 3.6.9 | Servidor Modbus TCP simulado |
