using MachineMonitor.Models;
using NModbus;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

/// <summary>
/// Implementação real do serviço Modbus usando NModbus + TCP.
///
/// Mapeamento de funções Modbus:
///   FC02 ReadDiscreteInputs → DiscreteInputs (EmergencyActive, MachineRunning)
///   FC04 ReadInputRegisters → InputRegisters (Temperature, Pressure, MotorSpeed, ProductionCounter)
///   FC05 WriteSingleCoil   → Coils (MachinePower, ResetAlarm)
///   FC06 WriteSingleRegister → HoldingRegisters (TemperatureSetpoint, MotorSpeedSetpoint)
/// </summary>
public class NModbusService : IModbusService
{
    private TcpClient? _client;
    private IModbusMaster? _master;
    private byte _unitId;
    private string _lastAlarmReason = "";

    public bool IsConnected { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port, byte unitId)
    {
        try
        {
            await DisconnectAsync();

            _unitId = unitId;
            _client = new TcpClient();

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _client.ConnectAsync(host, port, cts.Token);

            var factory = new ModbusFactory();
            _master = factory.CreateMaster(_client);
            _master.Transport.ReadTimeout  = 2000;
            _master.Transport.WriteTimeout = 2000;

            IsConnected = true;
            _lastAlarmReason = "";
            return true;
        }
        catch (Exception)
        {
            await DisconnectAsync();
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        _master?.Dispose();
        _client?.Close();
        _master = null;
        _client = null;
        return Task.CompletedTask;
    }

    public async Task<MachineData?> ReadMachineDataAsync()
    {
        if (_master == null) return null;

        try
        {
            // FC04 — lê 4 Input Registers a partir do endereço 0 (30001…30004)
            ushort[] inputRegs = await _master.ReadInputRegistersAsync(
                _unitId,
                (ushort)ModbusAddressMap.InputRegisters.Temperature,
                4);

            // FC02 — lê 2 Discrete Inputs a partir do endereço 0 (10001…10002)
            bool[] discreteInputs = await _master.ReadInputsAsync(
                _unitId,
                (ushort)ModbusAddressMap.DiscreteInputs.EmergencyActive,
                2);

            double temperature = inputRegs[ModbusAddressMap.InputRegisters.Temperature] / 10.0;
            double pressure    = inputRegs[ModbusAddressMap.InputRegisters.Pressure]    / 100.0;
            double motorSpeed  = inputRegs[ModbusAddressMap.InputRegisters.MotorSpeed];
            bool   alarmActive = discreteInputs[ModbusAddressMap.DiscreteInputs.EmergencyActive];
            bool   machineOn   = discreteInputs[ModbusAddressMap.DiscreteInputs.MachineRunning];

            // Persiste a razão enquanto o alarme estiver ativo.
            // O pico que disparou o alarme pode durar apenas um ciclo; sem cache,
            // os ciclos seguintes (valores normalizados) perderiam a causa original.
            string alarmReason = "";
            if (alarmActive)
            {
                string inferred = InferAlarmReason(temperature, pressure, machineOn, motorSpeed);
                if (!string.IsNullOrEmpty(inferred))
                    _lastAlarmReason = inferred;        // atualiza cache quando identifica
                alarmReason = _lastAlarmReason;         // usa cache como fallback
            }
            else
            {
                _lastAlarmReason = "";                  // limpa ao desativar o alarme
            }

            return new MachineData
            {
                Temperature     = temperature,
                Pressure        = pressure,
                MotorSpeed      = motorSpeed,
                ProductionCount = inputRegs[ModbusAddressMap.InputRegisters.ProductionCounter],
                AlarmActive     = alarmActive,
                MachineOn       = machineOn,
                AlarmReason     = alarmReason,
            };
        }
        catch (Exception)
        {
            // Conexão perdida inesperadamente
            IsConnected = false;
            return null;
        }
    }

    // Infere a razão do alarme a partir dos valores lidos — mesmos limiares do FakeModbusService
    private static string InferAlarmReason(double temp, double pressure, bool machineOn, double motorSpeed)
    {
        if (temp       > 95.0)             return $"Superaquecimento ({temp:F1} °C > 95 °C)";
        if (pressure   >  7.0)             return $"Sobrepressão ({pressure:F2} bar > 7,0 bar)";
        if (pressure   <  1.0)             return $"Baixa pressão ({pressure:F2} bar < 1,0 bar)";
        if (machineOn && motorSpeed < 200) return $"Falha de partida ({motorSpeed:F0} rpm < 200 rpm)";
        return "";
    }

    // ── Coil writes (FC05) ───────────────────────────────────────────────────

    public Task<bool> TurnOnMachineAsync() =>
        WriteCoilAsync(ModbusAddressMap.Coils.MachinePower, true);

    public Task<bool> TurnOffMachineAsync() =>
        WriteCoilAsync(ModbusAddressMap.Coils.MachinePower, false);

    public Task<bool> ResetAlarmAsync() =>
        WriteCoilAsync(ModbusAddressMap.Coils.ResetAlarm, true);

    // ── Holding register writes (FC06) ───────────────────────────────────────

    public async Task<bool> WriteSetpointsAsync(double temperatureSetpoint, double motorSpeedSetpoint)
    {
        // Temperatura: converte °C → °C × 10 (ex.: 75.0 → 750)
        bool ok1 = await WriteRegisterAsync(
            ModbusAddressMap.HoldingRegisters.TemperatureSetpoint,
            (ushort)(temperatureSetpoint * 10));

        // Velocidade: RPM direto
        bool ok2 = await WriteRegisterAsync(
            ModbusAddressMap.HoldingRegisters.MotorSpeedSetpoint,
            (ushort)motorSpeedSetpoint);

        return ok1 && ok2;
    }

    // ── Helpers internos ─────────────────────────────────────────────────────

    private async Task<bool> WriteCoilAsync(int address, bool value)
    {
        if (_master == null) return false;
        try
        {
            await _master.WriteSingleCoilAsync(_unitId, (ushort)address, value);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> WriteRegisterAsync(int address, ushort value)
    {
        if (_master == null) return false;
        try
        {
            await _master.WriteSingleRegisterAsync(_unitId, (ushort)address, value);
            return true;
        }
        catch { return false; }
    }
}
