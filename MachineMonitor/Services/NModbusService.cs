using MachineMonitor.Models;
using NModbus;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public class NModbusService : IModbusService
{
    private TcpClient?    _client;
    private IModbusMaster? _master;
    private string _lastHost   = "";
    private int    _lastPort;
    private byte   _lastUnitId;
    private byte   _unitId;
    private string _lastAlarmReason = "";

    public bool IsConnected { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port, byte unitId)
    {
        try
        {
            await DisconnectAsync();

            _lastHost   = host;
            _lastPort   = port;
            _lastUnitId = unitId;
            _unitId     = unitId;
            _client     = new TcpClient();

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _client.ConnectAsync(host, port, cts.Token);

            var factory = new ModbusFactory();
            _master = factory.CreateMaster(_client);
            _master.Transport.ReadTimeout  = 2000;
            _master.Transport.WriteTimeout = 2000;

            IsConnected      = true;
            _lastAlarmReason = "";
            return true;
        }
        catch (Exception)
        {
            await DisconnectAsync();
            return false;
        }
    }

    public Task<bool> ReconnectAsync() => ConnectAsync(_lastHost, _lastPort, _lastUnitId);

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
            ushort[] inputRegs = await _master.ReadInputRegistersAsync(
                _unitId,
                (ushort)ModbusAddressMap.InputRegisters.Temperature,
                4);

            bool[] discreteInputs = await _master.ReadInputsAsync(
                _unitId,
                (ushort)ModbusAddressMap.DiscreteInputs.EmergencyActive,
                2);

            double temperature = inputRegs[ModbusAddressMap.InputRegisters.Temperature] / 10.0;
            double pressure    = inputRegs[ModbusAddressMap.InputRegisters.Pressure]    / 100.0;
            double motorSpeed  = inputRegs[ModbusAddressMap.InputRegisters.MotorSpeed];
            bool   alarmActive = discreteInputs[ModbusAddressMap.DiscreteInputs.EmergencyActive];
            bool   machineOn   = discreteInputs[ModbusAddressMap.DiscreteInputs.MachineRunning];

            // Cache da razão do alarme crítico: o pico dura só 1 ciclo no Python
            string alarmReason = "";
            if (alarmActive)
            {
                string inferred = InferAlarmReason(temperature, pressure, machineOn, motorSpeed);
                if (!string.IsNullOrEmpty(inferred))
                    _lastAlarmReason = inferred;
                alarmReason = _lastAlarmReason;
            }
            else
            {
                _lastAlarmReason = "";
            }

            // Aviso (auto-clearing) — só quando sem alarme crítico
            bool   warningActive = false;
            string warningReason = "";
            if (!alarmActive)
            {
                warningReason = InferWarningReason(temperature, pressure, machineOn, motorSpeed);
                warningActive = !string.IsNullOrEmpty(warningReason);
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
                WarningActive   = warningActive,
                WarningReason   = warningReason,
            };
        }
        catch (Exception)
        {
            IsConnected = false;
            return null;
        }
    }

    private static string InferAlarmReason(double temp, double pressure, bool machineOn, double motorSpeed)
    {
        if (temp       > 95.0)             return $"Superaquecimento ({temp:F1} °C > 95 °C)";
        if (pressure   >  7.0)             return $"Sobrepressão ({pressure:F2} bar > 7,0 bar)";
        if (pressure   <  1.0)             return $"Baixa pressão ({pressure:F2} bar < 1,0 bar)";
        if (machineOn && motorSpeed < 200) return $"Falha de partida ({motorSpeed:F0} rpm < 200 rpm)";
        return "";
    }

    private static string InferWarningReason(double temp, double pressure, bool machineOn, double motorSpeed)
    {
        if (temp       > 85.0)             return $"Temp. elevada ({temp:F1} °C > 85 °C)";
        if (pressure   >  6.0)             return $"Pressão alta ({pressure:F2} bar > 6,0 bar)";
        if (pressure   <  1.5)             return $"Pressão baixa ({pressure:F2} bar < 1,5 bar)";
        if (machineOn && motorSpeed < 300) return $"Velocidade baixa ({motorSpeed:F0} rpm < 300 rpm)";
        return "";
    }

    public Task<bool> TurnOnMachineAsync()  => WriteCoilAsync(ModbusAddressMap.Coils.MachinePower, true);
    public Task<bool> TurnOffMachineAsync() => WriteCoilAsync(ModbusAddressMap.Coils.MachinePower, false);
    public Task<bool> ResetAlarmAsync()     => WriteCoilAsync(ModbusAddressMap.Coils.ResetAlarm,   true);

    public async Task<bool> WriteSetpointsAsync(double temperatureSetpoint, double motorSpeedSetpoint)
    {
        bool ok1 = await WriteRegisterAsync(
            ModbusAddressMap.HoldingRegisters.TemperatureSetpoint,
            (ushort)(temperatureSetpoint * 10));
        bool ok2 = await WriteRegisterAsync(
            ModbusAddressMap.HoldingRegisters.MotorSpeedSetpoint,
            (ushort)motorSpeedSetpoint);
        return ok1 && ok2;
    }

    private async Task<bool> WriteCoilAsync(int address, bool value)
    {
        if (_master == null) return false;
        try { await _master.WriteSingleCoilAsync(_unitId, (ushort)address, value); return true; }
        catch { return false; }
    }

    private async Task<bool> WriteRegisterAsync(int address, ushort value)
    {
        if (_master == null) return false;
        try { await _master.WriteSingleRegisterAsync(_unitId, (ushort)address, value); return true; }
        catch { return false; }
    }
}
