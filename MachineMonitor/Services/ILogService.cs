using MachineMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public interface ILogService
{
    ObservableCollection<MachineLogEntry> Entries { get; }
    void Add(LogEventType type, string message);
    Task ExportToCsvAsync(string filePath);

    // Leituras de sensores (para gráfico e exportação CSV de dados)
    event EventHandler? ReadingAdded;
    void AddReading(SensorReading reading);
    IReadOnlyList<SensorReading> GetReadings();
    Task ExportReadingsToCsvAsync(string filePath);
}
