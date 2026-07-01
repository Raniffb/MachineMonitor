using MachineMonitor.Models;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public enum ConnectionMode { Simulated, ModbusTcp }

/// <summary>
/// Proxy que delega todas as chamadas a <see cref="FakeModbusService"/> ou
/// <see cref="NModbusService"/> conforme o modo escolhido na UI.
/// Registrado como Singleton de <see cref="IModbusService"/> no DI container,
/// portanto ConnectionViewModel e DashboardViewModel compartilham a mesma instância.
/// </summary>
public class ModbusServiceProxy : IModbusService
{
    private readonly FakeModbusService _fake;
    private readonly NModbusService _real;
    private IModbusService _current;

    public ConnectionMode CurrentMode { get; private set; } = ConnectionMode.Simulated;

    public ModbusServiceProxy(FakeModbusService fake, NModbusService real)
    {
        _fake = fake;
        _real = real;
        _current = fake;
    }

    public void SetMode(ConnectionMode mode)
    {
        CurrentMode = mode;
        _current = mode == ConnectionMode.ModbusTcp ? _real : _fake;
    }

    // ── IModbusService delegation ─────────────────────────────────────────────

    public bool IsConnected => _current.IsConnected;

    public Task<bool> ConnectAsync(string host, int port, byte unitId) =>
        _current.ConnectAsync(host, port, unitId);

    public Task DisconnectAsync() => _current.DisconnectAsync();

    public Task<MachineData?> ReadMachineDataAsync() => _current.ReadMachineDataAsync();

    public Task<bool> TurnOnMachineAsync()  => _current.TurnOnMachineAsync();
    public Task<bool> TurnOffMachineAsync() => _current.TurnOffMachineAsync();
    public Task<bool> ResetAlarmAsync()     => _current.ResetAlarmAsync();

    public Task<bool> WriteSetpointsAsync(double temperatureSetpoint, double motorSpeedSetpoint) =>
        _current.WriteSetpointsAsync(temperatureSetpoint, motorSpeedSetpoint);

    public Task<bool> ReconnectAsync() => _current.ReconnectAsync();
}
