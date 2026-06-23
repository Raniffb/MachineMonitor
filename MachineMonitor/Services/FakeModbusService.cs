using MachineMonitor.Models;
using System;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public class FakeModbusService : IModbusService
{
    private readonly Random _rng = new();
    private int _productionCount = 0;

    // Estado controlado por comandos
    private bool? _forcedMachineOn = null;

    // Alarme latchado: dispara por condição, só limpa via ResetAlarmAsync
    private bool   _alarmLatched          = false;
    private string _alarmReason           = "";
    private DateTime _alarmSuppressedUntil = DateTime.MinValue;

    // Setpoints
    private double _temperatureBase = 75.0;
    private double _motorSpeedBase  = 1450.0;

    // ── Condições de alarme (espelhadas no tooltip do DashboardView) ──────────
    // • Temp     > 95 °C          — Superaquecimento
    // • Pressão  > 7,0 bar        — Sobrepressão
    // • Pressão  < 1,0 bar        — Baixa pressão
    // • Máq. ON e Motor < 200 rpm — Falha de partida
    private const double AlarmTempHigh    =  95.0;
    private const double AlarmPressHigh   =   7.0;
    private const double AlarmPressLow    =   1.0;
    private const double AlarmMotorLow    = 200.0;

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

        // Oscilação normal em volta dos setpoints
        double temperature = _temperatureBase + (_rng.NextDouble() - 0.5) * 10.0;
        double pressure    = 4.5              + _rng.NextDouble() * 1.5;
        double motorSpeed  = machineOn
            ? _motorSpeedBase + (_rng.NextDouble() - 0.5) * 200.0
            : 0;

        // Perturbação de processo: 2 % de chance por ciclo (≈1 vez a cada 50 s).
        // Provoca um pico que viola uma das condições de alarme.
        if (!_alarmLatched && DateTime.Now > _alarmSuppressedUntil && _rng.NextDouble() < 0.02)
        {
            switch (_rng.Next(4))
            {
                case 0: temperature += 20.0 + _rng.NextDouble() * 15.0; break; // 95–110 °C
                case 1: pressure    +=  3.0 + _rng.NextDouble() *  2.0; break; // 7–9 bar
                case 2: pressure    -=  3.5 + _rng.NextDouble() *  1.0; break; // 0–1 bar
                case 3: if (machineOn) motorSpeed = _rng.NextDouble() * 150.0; break; // stall motor
            }
        }

        // Verifica condições somente fora do período de supressão pós-reset
        if (!_alarmLatched && DateTime.Now > _alarmSuppressedUntil)
        {
            if (temperature > AlarmTempHigh)
                Latch($"Superaquecimento ({temperature:F1} °C > {AlarmTempHigh} °C)");
            else if (pressure > AlarmPressHigh)
                Latch($"Sobrepressão ({pressure:F2} bar > {AlarmPressHigh:F1} bar)");
            else if (pressure < AlarmPressLow)
                Latch($"Baixa pressão ({pressure:F2} bar < {AlarmPressLow:F1} bar)");
            else if (machineOn && motorSpeed < AlarmMotorLow)
                Latch($"Falha de partida ({motorSpeed:F0} rpm < {AlarmMotorLow:F0} rpm)");
        }

        return new MachineData
        {
            Temperature    = Math.Round(temperature, 1),
            Pressure       = Math.Round(pressure,    2),
            MotorSpeed     = Math.Round(Math.Max(0, motorSpeed), 0),
            MachineOn      = machineOn,
            AlarmActive    = _alarmLatched,
            AlarmReason    = _alarmLatched ? _alarmReason : "",
            ProductionCount = _productionCount
        };
    }

    private void Latch(string reason)
    {
        _alarmLatched    = true;
        _alarmReason     = reason;
        _forcedMachineOn = false;   // safe state: desliga ao alarmar
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
