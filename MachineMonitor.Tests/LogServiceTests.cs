using MachineMonitor.Models;
using MachineMonitor.Services;

namespace MachineMonitor.Tests;

// Usa um caminho de arquivo isolado em cada teste (via o construtor internal),
// evitando tocar no %AppData% real do usuário.
public class LogServiceTests : IDisposable
{
    private readonly string _readingsFilePath =
        Path.Combine(Path.GetTempPath(), $"machinemonitor_test_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_readingsFilePath))
            File.Delete(_readingsFilePath);
    }

    [Fact]
    public void GetReadings_WhenNoFileExists_ReturnsEmpty()
    {
        var service = new LogService(_readingsFilePath);

        Assert.Empty(service.GetReadings());
    }

    [Fact]
    public void AddReading_PersistsToDisk_AndReloadsInNewInstance()
    {
        var service = new LogService(_readingsFilePath);
        var reading = new SensorReading
        {
            Timestamp       = new DateTime(2026, 1, 1, 12, 0, 0),
            Temperature     = 78.5,
            Pressure        = 4.9,
            MotorSpeed      = 1440,
            ProductionCount = 42,
            MachineOn       = true,
            AlarmActive     = false,
        };

        service.AddReading(reading);

        Assert.True(File.Exists(_readingsFilePath));

        // Nova instância, mesmo arquivo — simula reabrir o app
        var reloaded = new LogService(_readingsFilePath);
        var readings = reloaded.GetReadings();

        Assert.Single(readings);
        Assert.Equal(reading.Temperature, readings[0].Temperature);
        Assert.Equal(reading.ProductionCount, readings[0].ProductionCount);
    }

    [Fact]
    public void AddReading_RaisesReadingAddedEvent()
    {
        var service = new LogService(_readingsFilePath);
        int eventCount = 0;
        service.ReadingAdded += (_, _) => eventCount++;

        service.AddReading(new SensorReading { Timestamp = DateTime.Now });

        Assert.Equal(1, eventCount);
    }
}
