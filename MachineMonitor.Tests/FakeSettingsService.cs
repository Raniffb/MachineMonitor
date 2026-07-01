using MachineMonitor.Models;
using MachineMonitor.Services;

namespace MachineMonitor.Tests;

// Implementação mínima de ISettingsService para testes, sem tocar o %AppData% real do usuário.
internal class FakeSettingsService : ISettingsService
{
    public AppSettings Current { get; private set; } = new();

    public AppSettings Load() => Current;

    public void Save(AppSettings settings) => Current = settings;
}
