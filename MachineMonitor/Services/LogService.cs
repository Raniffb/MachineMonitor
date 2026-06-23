using Avalonia.Media;
using Avalonia.Threading;
using MachineMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public class LogService : ILogService
{
    private const int MaxEntries = 100;

    private static readonly Dictionary<LogEventType, (string Label, IBrush Brush)> _meta = new()
    {
        [LogEventType.Connected]       = ("Conectado",       new SolidColorBrush(Color.Parse("#00C853"))),
        [LogEventType.Disconnected]    = ("Desconectado",    new SolidColorBrush(Color.Parse("#4A5568"))),
        [LogEventType.CommError]       = ("Erro Comm.",      new SolidColorBrush(Color.Parse("#FF5252"))),
        [LogEventType.MachineOn]       = ("Máq. Ligada",     new SolidColorBrush(Color.Parse("#00B4D8"))),
        [LogEventType.MachineOff]      = ("Máq. Desligada",  new SolidColorBrush(Color.Parse("#FF9800"))),
        [LogEventType.AlarmTriggered]  = ("ALARME!",         new SolidColorBrush(Color.Parse("#FF3D3D"))),
        [LogEventType.AlarmReset]      = ("Alarm Reset",     new SolidColorBrush(Color.Parse("#FFB800"))),
        [LogEventType.SetpointChanged] = ("Setpoint",        new SolidColorBrush(Color.Parse("#7B61FF"))),
    };

    public ObservableCollection<MachineLogEntry> Entries { get; } = new();

    public void Add(LogEventType type, string message)
    {
        var (label, brush) = _meta.TryGetValue(type, out var m) ? m : (type.ToString(), (IBrush)Brushes.Gray);

        var entry = new MachineLogEntry
        {
            Timestamp  = DateTime.Now,
            EventType  = type,
            Message    = message,
            BadgeLabel = label,
            BadgeBrush = brush,
        };

        Dispatcher.UIThread.Post(() =>
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(Entries.Count - 1);
        });
    }

    public async Task ExportToCsvAsync(string filePath)
    {
        var lines = new List<string> { "Timestamp,EventType,Tipo,Mensagem" };

        foreach (var e in Entries.Reverse())
        {
            string safe = e.Message.Replace("\"", "\"\"");
            lines.Add($"{e.Timestamp:yyyy-MM-dd HH:mm:ss},{e.EventType},{e.BadgeLabel},\"{safe}\"");
        }

        await File.WriteAllLinesAsync(filePath, lines, System.Text.Encoding.UTF8);
    }
}
