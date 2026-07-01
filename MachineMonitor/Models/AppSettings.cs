namespace MachineMonitor.Models;

public class AppSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public string Port { get; set; } = "5020";
    public string UnitId { get; set; } = "1";
    public string Mode { get; set; } = "Simulado";
}
