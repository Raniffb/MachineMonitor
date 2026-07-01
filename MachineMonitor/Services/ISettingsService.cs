using MachineMonitor.Models;

namespace MachineMonitor.Services;

public interface ISettingsService
{
    // Configuração em memória, sempre em sincronia com o que foi salvo por último
    // (usada pelos serviços Modbus para ler limiares atualizados sem precisar reiniciar o app).
    AppSettings Current { get; }

    AppSettings Load();
    void Save(AppSettings settings);
}
