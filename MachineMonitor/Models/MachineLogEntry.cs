using Avalonia.Media;
using System;

namespace MachineMonitor.Models;

public enum LogEventType
{
    Connected,
    Disconnected,
    CommError,
    MachineOn,
    MachineOff,
    AlarmTriggered,
    AlarmReset,
    SetpointChanged,
}

public class MachineLogEntry
{
    public DateTime Timestamp { get; init; }
    public LogEventType EventType { get; init; }
    public string Message { get; init; } = "";
    public string BadgeLabel { get; init; } = "";
    public IBrush BadgeBrush { get; init; } = Brushes.Gray;
}
