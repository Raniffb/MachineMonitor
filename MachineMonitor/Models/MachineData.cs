namespace MachineMonitor.Models;

public class MachineData
{
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double MotorSpeed { get; set; }
    public bool MachineOn { get; set; }
    public bool AlarmActive { get; set; }
    public int ProductionCount { get; set; }

    // Descrição da condição que ativou o alarme (vazio quando sem alarme)
    public string AlarmReason { get; set; } = "";
}
