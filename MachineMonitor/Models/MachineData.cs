namespace MachineMonitor.Models;

public class MachineData
{
    public double Temperature    { get; set; }
    public double Pressure       { get; set; }
    public double MotorSpeed     { get; set; }
    public bool   MachineOn      { get; set; }
    public int    ProductionCount { get; set; }

    // Alarme crítico (latchado — requer reset manual)
    public bool   AlarmActive  { get; set; }
    public string AlarmReason  { get; set; } = "";

    // Pré-alarme (auto-clearing — some quando a condição normaliza)
    public bool   WarningActive { get; set; }
    public string WarningReason { get; set; } = "";
}
