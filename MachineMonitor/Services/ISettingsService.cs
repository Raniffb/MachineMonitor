using MachineMonitor.Models;

namespace MachineMonitor.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
