using MachineMonitor.Models;
using System;
using System.IO;
using System.Text.Json;

namespace MachineMonitor.Services;

public class SettingsService : ISettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MachineMonitor", "settings.json");

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        Current = LoadFromDisk();
    }

    public AppSettings Load() => Current;

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        Current = settings;
    }

    private static AppSettings LoadFromDisk()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }
}
