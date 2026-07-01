using System;

namespace MachineMonitor.Models;

public class SensorReading
{
    public DateTime Timestamp    { get; init; }
    public double Temperature    { get; init; }
    public double Pressure       { get; init; }
    public double MotorSpeed     { get; init; }
    public int    ProductionCount { get; init; }
    public bool   MachineOn      { get; init; }
    public bool   AlarmActive    { get; init; }
}
