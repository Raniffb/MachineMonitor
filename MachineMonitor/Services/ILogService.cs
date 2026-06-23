using MachineMonitor.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public interface ILogService
{
    ObservableCollection<MachineLogEntry> Entries { get; }
    void Add(LogEventType type, string message);
    Task ExportToCsvAsync(string filePath);
}
