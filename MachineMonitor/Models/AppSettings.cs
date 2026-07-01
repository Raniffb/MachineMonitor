namespace MachineMonitor.Models;

public class AppSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public string Port { get; set; } = "5020";
    public string UnitId { get; set; } = "1";
    public string Mode { get; set; } = "Simulado";

    // ── Limiares críticos (disparam alarme latchado) ──────────────────────────
    public double CriticalTempHigh  { get; set; } = 95.0;
    public double CriticalPressHigh { get; set; } = 7.0;
    public double CriticalPressLow  { get; set; } = 1.0;
    public double CriticalMotorLow  { get; set; } = 200.0;

    // ── Limiares de aviso (auto-clearing, abaixo do crítico) ─────────────────
    public double WarnTempHigh  { get; set; } = 85.0;
    public double WarnPressHigh { get; set; } = 6.0;
    public double WarnPressLow  { get; set; } = 1.5;
    public double WarnMotorLow  { get; set; } = 300.0;
}
