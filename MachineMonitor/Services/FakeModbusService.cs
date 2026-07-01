using MachineMonitor.Models;
using System;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public class FakeModbusService : IModbusService
{
    private readonly Random _rng = new();
    private int _productionCount = 0;

    private bool? _forcedMachineOn = null;

    // Alarme latchado (crítico): dispara por condição, só limpa via ResetAlarmAsync
    private bool     _alarmLatched          = false;
    private string   _alarmReason           = "";
    private DateTime _alarmSuppressedUntil   = DateTime.MinValue;

    // Setpoints
    private double _temperatureBase = 75.0;
    private double _motorSpeedBase  = 1450.0;

    // ── Limiares críticos (disparam alarme latchado) ──────────────────────────
    private const double AlarmTempHigh  =  95.0;
    private const double AlarmPressHigh =   7.0;
    private const double AlarmPressLow  =   1.0;
    private const double AlarmMotorLow  = 200.0;

    // ── Limiares de aviso (auto-clearing, abaixo do crítico) ─────────────────
    private const double WarnTempHigh  =  85.0;
    private const double WarnPressHigh =   6.0;
    private const double WarnPressLow  =   1.5;
    private const double WarnMotorLow  = 300.0;

    public bool IsConnected { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port, byte unitId)
    {
        await Task.Delay(800);
        IsConnected           = true;
        _forcedMachineOn      = null;
        _alarmLatched         = false;
        _alarmReason          = "";
        _alarmSuppressedUntil = DateTime.MinValue;
        _productionCount      = 0;
        return true;
    }

    public Task<bool> ReconnectAsync() => ConnectAsync("", 0, 0);

    public async Task DisconnectAsync()
    {
        await Task.Delay(300);
        IsConnected = false;
    }

    public async Task<MachineData?> ReadMachineDataAsync()
    {
        if (!IsConnected) return null;
        await Task.Delay(50);

        bool machineOn = _forcedMachineOn ?? (_rng.NextDouble() > 0.05);
        if (machineOn) _productionCount++;

        double temperature = _temperatureBase + (_rng.NextDouble() - 0.5) * 10.0;
        double pressure    = 4.5              + _rng.NextDouble() * 1.5;
        double motorSpeed  = machineOn
            ? _motorSpeedBase + (_rng.NextDouble() - 0.5) * 200.0
            : 0;

        // Perturbação de processo: 2 % de chance por ciclo (≈1 vez a cada 50 s)
        if (!_alarmLatched && DateTime.Now > _alarmSuppressedUntil && _rng.NextDouble() < 0.02)
        {
            switch (_rng.Next(4))
            {
                case 0: temperature += 20.0 + _rng.NextDouble() * 15.0; break;
                case 1: pressure    +=  3.0 + _rng.NextDouble() *  2.0; break;
                case 2: pressure    -=  3.5 + _rng.NextDouble() *  1.0; break;
                case 3: if (machineOn) motorSpeed = _rng.NextDouble() * 150.0; break;
            }
        }

        // Verifica condições críticas (latchado)
        if (!_alarmLatched && DateTime.Now > _alarmSuppressedUntil)
        {
            if      (temperature > AlarmTempHigh)
                Latch($"Superaquecimento ({temperature:F1} °C > {AlarmTempHigh} °C)");
            else if (pressure > AlarmPressHigh)
                Latch($"Sobrepressão ({pressure:F2} bar > {AlarmPressHigh:F1} bar)");
            else if (pressure < AlarmPressLow)
                Latch($"Baixa pressão ({pressure:F2} bar < {AlarmPressLow:F1} bar)");
            else if (machineOn && motorSpeed < AlarmMotorLow)
                Latch($"Falha de partida ({motorSpeed:F0} rpm < {AlarmMotorLow:F0} rpm)");
        }

        // Aviso (auto-clearing) — só avalia quando sem alarme crítico ativo
        bool   warningActive = false;
        string warningReason = "";
        if (!_alarmLatched)
        {
            if      (temperature > WarnTempHigh)
            { warningActive = true; warningReason = $"Temp. elevada ({temperature:F1} °C > {WarnTempHigh} °C)"; }
            else if (pressure > WarnPressHigh)
            { warningActive = true; warningReason = $"Pressão alta ({pressure:F2} bar > {WarnPressHigh:F1} bar)"; }
            else if (pressure < WarnPressLow)
            { warningActive = true; warningReason = $"Pressão baixa ({pressure:F2} bar < {WarnPressLow:F1} bar)"; }
            else if (machineOn && motorSpeed < WarnMotorLow)
            { warningActive = true; warningReason = $"Velocidade baixa ({motorSpeed:F0} rpm < {WarnMotorLow:F0} rpm)"; }
        }

        return new MachineData
        {
            Temperature     = Math.Round(temperature, 1),
            Pressure        = Math.Round(pressure,    2),
            MotorSpeed      = Math.Round(Math.Max(0, motorSpeed), 0),
            MachineOn       = machineOn,
            AlarmActive     = _alarmLatched,
            AlarmReason     = _alarmLatched ? _alarmReason : "",
            WarningActive   = warningActive,
            WarningReason   = warningReason,
            ProductionCount = _productionCount,
        };
    }

    private void Latch(string reason)
    {
        _alarmLatched    = true;
        _alarmReason     = reason;
        _forcedMachineOn = false;
    }

    public async Task<bool> TurnOnMachineAsync()
    {
        await Task.Delay(120);
        _forcedMachineOn = true;
        return true;
    }

    public async Task<bool> TurnOffMachineAsync()
    {
        await Task.Delay(120);
        _forcedMachineOn = false;
        return true;
    }

    public async Task<bool> ResetAlarmAsync()
    {
        await Task.Delay(120);
        _alarmLatched         = false;
        _alarmReason          = "";
        _alarmSuppressedUntil = DateTime.Now.AddSeconds(3);
        return true;
    }

    public async Task<bool> WriteSetpointsAsync(double temperatureSetpoint, double motorSpeedSetpoint)
    {
        await Task.Delay(150);
        _temperatureBase = temperatureSetpoint;
        _motorSpeedBase  = motorSpeedSetpoint;
        return true;
    }
}
