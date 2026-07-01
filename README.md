# MachineMonitor

[![CI](https://github.com/Raniffb/MachineMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/Raniffb/MachineMonitor/actions/workflows/ci.yml)

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
- Cards de status da máquina (LIGADA / DESLIGADA) e de alarme (CRÍTICO / AVISO / INATIVO)
- Exibição da causa do alarme com persistência de cache (evita perda de motivo em picos transientes)
- Indicador de atualização ao vivo com timestamp
- **Reconexão automática**: após 3 falhas de leitura consecutivas, tenta reconectar (até 3 tentativas) antes de encerrar a sessão

### Tendências
- Gráfico histórico (via LiveCharts) de temperatura, pressão e velocidade do motor com base nas últimas leituras registradas

### Controles do operador
- Ligar e desligar a máquina
- Resetar alarme manualmente
- Enviar setpoints de temperatura (°C) e velocidade do motor (RPM)

### Sistema de alarme (dois níveis)
| Nível | Condição | Limiar | Causa |
|-------|----------|--------|-------|
| Crítico (latchado) | Temperatura alta | > 95 °C | Superaquecimento |
| Crítico (latchado) | Pressão alta | > 7,0 bar | Sobrepressão |
| Crítico (latchado) | Pressão baixa | < 1,0 bar | Baixa pressão |
| Crítico (latchado) | Velocidade baixa (máquina ligada) | < 200 rpm | Falha de partida |
| Aviso (auto-clearing) | Temperatura elevada | > 85 °C | Temp. elevada |
| Aviso (auto-clearing) | Pressão alta | > 6,0 bar | Pressão alta |
| Aviso (auto-clearing) | Pressão baixa | < 1,5 bar | Pressão baixa |
| Aviso (auto-clearing) | Velocidade baixa (máquina ligada) | < 300 rpm | Velocidade baixa |

- **Alarme crítico** é latchado: permanece ativo até reset manual do operador
- **Aviso** é auto-clearing: some sozinho assim que a condição normaliza, e só é avaliado quando não há alarme crítico ativo
- **Safe state**: ao disparar o alarme crítico, a máquina é desligada automaticamente
- **Janela de supressão** de 3 segundos após reset (evita redisparo imediato)
- Fluxo do operador: alarme crítico dispara → máquina para → operador reseta → operador religa
- **Limiares configuráveis**: a seção "Limiares de Alarme" na tela de conexão permite editar os 8 valores acima. São salvos em `settings.json` e aplicados imediatamente (lidos a cada leitura), sem precisar reconectar

### Log de eventos e exportação
- Registra automaticamente: disparo de alarme, reset de alarme, máquina ligada/desligada, alteração de setpoints e erros de comunicação
- **Exportar Eventos**: exporta o log de eventos para CSV
- **Exportar Leituras**: exporta o histórico de leituras de sensores (até 1000 amostras) para CSV
- O histórico de leituras é **persistido em `%AppData%/MachineMonitor/readings.json`**, sobrevivendo a reinícios do app — a tela de Tendências carrega esse histórico ao abrir

### Configuração de conexão
- A última configuração usada com sucesso (host, porta, unit ID e modo) é salva em `%AppData%/MachineMonitor/settings.json` e recarregada automaticamente na próxima abertura do app

---

## Estrutura do projeto

```
MachineMonitor/
├── Models/
│   ├── MachineData.cs          — DTO com os dados lidos da máquina (inclui alarme e aviso)
│   ├── SensorReading.cs        — amostra histórica para gráfico e exportação
│   ├── AppSettings.cs          — configuração de conexão persistida
│   └── ModbusAddressMap.cs     — mapa de endereços Modbus
├── Services/
│   ├── IModbusService.cs       — interface de comunicação (inclui ReconnectAsync)
│   ├── FakeModbusService.cs    — implementação simulada (sem rede)
│   ├── NModbusService.cs       — implementação real via NModbus TCP
│   ├── ModbusServiceProxy.cs   — proxy singleton (seleciona Fake ou TCP)
│   ├── LogService.cs           — log de eventos e histórico de leituras
│   └── ISettingsService.cs / SettingsService.cs — persistência de settings.json
├── ViewModels/
│   ├── ConnectionViewModel.cs  — tela de conexão, seleção de modo e settings
│   ├── DashboardViewModel.cs   — lógica do dashboard (poll loop, comandos, reconexão)
│   ├── TrendsViewModel.cs      — dados do gráfico de tendências
│   └── LogViewModel.cs         — histórico de eventos e exportações CSV
├── Views/
│   ├── ConnectionView.axaml    — interface de conexão
│   ├── DashboardView.axaml     — interface do dashboard
│   ├── TrendsView.axaml        — gráfico de tendências
│   └── LogView.axaml           — interface de log de eventos
MachineMonitor.Tests/            — testes unitários (xUnit)
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
  - `Avalonia.Fonts.Inter 11.2.3`
  - `Avalonia.ReactiveUI 11.2.3`
  - `CommunityToolkit.Mvvm 8.3.2`
  - `LiveChartsCore.SkiaSharpView.Avalonia 2.0.5`
  - `Microsoft.Extensions.DependencyInjection 9.0.0`
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

## Testes

O projeto `MachineMonitor.Tests` (xUnit) cobre os limiares de alarme/aviso — inclusive customizados (`AlarmThresholds`) —, o ciclo de vida do alarme crítico no `FakeModbusService` (disparo → safe state → reset → janela de supressão) e a persistência do histórico de leituras (`LogService`), usando um caminho de arquivo isolado por teste.

```bash
dotnet test
```

---

## Servidor Python — detalhes

O `modbus_server.py` simula a dinâmica real de uma máquina industrial:

- **Oscilação normal**: temperatura ±5 °C em torno do setpoint, pressão entre 4,5–6,0 bar, motor ±100 rpm
- **Perturbações de processo**: 0,1% de chance por ciclo (≈ 1 evento a cada 1000 s), sorteando aleatoriamente entre superaquecimento, sobrepressão, baixa pressão ou stall do motor
- **Safe state**: ao alarmar, o coil de energia da máquina é desligado automaticamente
- **Console**: exibe linha atualizada em tempo real com todos os valores e status de alarme

---

## Tecnologias utilizadas

| Tecnologia | Uso |
|------------|-----|
| C# / .NET 8 | Linguagem principal |
| Avalonia UI 11.2.3 | Interface gráfica multiplataforma |
| CommunityToolkit.Mvvm | MVVM com source generators |
| LiveChartsCore.SkiaSharpView.Avalonia | Gráfico de tendências |
| NModbus 3.0.81 | Cliente Modbus TCP |
| Python 3 + pymodbus 3.6.9 | Servidor Modbus TCP simulado |
