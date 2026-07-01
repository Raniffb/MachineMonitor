using MachineMonitor.Services;

namespace MachineMonitor.Tests;

public class FakeModbusServiceTests
{
    [Fact]
    public async Task ReadMachineDataAsync_WhenNotConnected_ReturnsNull()
    {
        var service = new FakeModbusService(new FakeSettingsService());

        var data = await service.ReadMachineDataAsync();

        Assert.Null(data);
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        var service = new FakeModbusService(new FakeSettingsService());

        bool ok = await service.ConnectAsync("host", 502, 1);

        Assert.True(ok);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_SetsIsConnectedFalse()
    {
        var service = new FakeModbusService(new FakeSettingsService());
        await service.ConnectAsync("host", 502, 1);

        await service.DisconnectAsync();

        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task TurnOnMachineAsync_ForcesMachineOnInSubsequentReadings()
    {
        var service = new FakeModbusService(new FakeSettingsService());
        await service.ConnectAsync("host", 502, 1);

        await service.TurnOnMachineAsync();
        var data = await service.ReadMachineDataAsync();

        Assert.NotNull(data);
        Assert.True(data!.MachineOn);
    }

    [Fact]
    public async Task TurnOffMachineAsync_ForcesMachineOffAndZeroMotorSpeed()
    {
        var service = new FakeModbusService(new FakeSettingsService());
        await service.ConnectAsync("host", 502, 1);

        await service.TurnOffMachineAsync();
        var data = await service.ReadMachineDataAsync();

        Assert.NotNull(data);
        Assert.False(data!.MachineOn);
        Assert.Equal(0, data.MotorSpeed);
    }

    // Motor stall (< 200 rpm com máquina ligada) é garantido de forma determinística
    // ao zerar o setpoint de velocidade: a oscilação (±100 rpm em torno do setpoint)
    // nunca alcança o limiar crítico de 200 rpm.
    [Fact]
    public async Task CriticalAlarm_MotorStall_LatchesAndForcesSafeState()
    {
        var service = new FakeModbusService(new FakeSettingsService());
        await service.ConnectAsync("host", 502, 1);
        await service.WriteSetpointsAsync(75, motorSpeedSetpoint: 0);
        await service.TurnOnMachineAsync();

        var firstRead = await service.ReadMachineDataAsync();
        Assert.NotNull(firstRead);
        Assert.True(firstRead!.AlarmActive);
        Assert.Contains("Falha de partida", firstRead.AlarmReason);
        Assert.False(firstRead.WarningActive);

        // Estado seguro: a máquina é desligada automaticamente a partir da próxima leitura
        var secondRead = await service.ReadMachineDataAsync();
        Assert.NotNull(secondRead);
        Assert.False(secondRead!.MachineOn);
        Assert.True(secondRead.AlarmActive);
    }

    [Fact]
    public async Task ResetAlarmAsync_ClearsAlarmAndSuppressesImmediateRetrigger()
    {
        var service = new FakeModbusService(new FakeSettingsService());
        await service.ConnectAsync("host", 502, 1);
        await service.WriteSetpointsAsync(75, motorSpeedSetpoint: 0);
        await service.TurnOnMachineAsync();
        await service.ReadMachineDataAsync(); // dispara o alarme crítico

        await service.ResetAlarmAsync();
        var data = await service.ReadMachineDataAsync();

        Assert.NotNull(data);
        Assert.False(data!.AlarmActive);
    }
}
