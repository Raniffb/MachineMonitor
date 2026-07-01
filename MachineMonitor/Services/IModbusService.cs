using MachineMonitor.Models;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public interface IModbusService
{
    bool IsConnected { get; }

    Task<bool> ConnectAsync(string host, int port, byte unitId);
    Task DisconnectAsync();
    Task<MachineData?> ReadMachineDataAsync();

    // Coil writes (FC05) — ModbusAddressMap.Coils
    Task<bool> TurnOnMachineAsync();
    Task<bool> TurnOffMachineAsync();
    Task<bool> ResetAlarmAsync();

    // Holding register writes (FC06) — ModbusAddressMap.HoldingRegisters
    Task<bool> WriteSetpointsAsync(double temperatureSetpoint, double motorSpeedSetpoint);

    // Reconecta usando os parâmetros da última ConnectAsync
    Task<bool> ReconnectAsync();
}
