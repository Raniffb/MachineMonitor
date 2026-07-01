using Avalonia.Media;
using Avalonia.Threading;
using MachineMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MachineMonitor.Services;

public class LogService : ILogService
{
    private const int MaxEntries  = 100;
    private const int MaxReadings = 1000;

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

    // ── Eventos ──────────────────────────────────────────────────────────────
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
        await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);
    }

    // ── Leituras de sensores ──────────────────────────────────────────────────
    public event EventHandler? ReadingAdded;

    private readonly List<SensorReading> _readings = new();

    public void AddReading(SensorReading reading)
    {
        lock (_readings)
        {
            _readings.Add(reading);
            if (_readings.Count > MaxReadings)
                _readings.RemoveAt(0);
        }
        ReadingAdded?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<SensorReading> GetReadings()
    {
        lock (_readings) { return _readings.ToList(); }
    }

    public async Task ExportReadingsToCsvAsync(string filePath)
    {
        IReadOnlyList<SensorReading> snapshot = GetReadings();
        var inv = CultureInfo.InvariantCulture;  // garante ponto como separador decimal
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        await writer.WriteLineAsync("Timestamp,Temperatura (C),Pressao (bar),Velocidade (rpm),Producao,Maquina,Alarme");
        foreach (var r in snapshot)
            await writer.WriteLineAsync(
                $"{r.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                $"{r.Temperature.ToString("F1", inv)}," +
                $"{r.Pressure.ToString("F2", inv)}," +
                $"{r.MotorSpeed.ToString("F0", inv)}," +
                $"{r.ProductionCount}," +
                $"{(r.MachineOn ? "Ligada" : "Desligada")}," +
                $"{(r.AlarmActive ? "Sim" : "Nao")}");
    }
}
