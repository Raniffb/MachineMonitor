using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MachineMonitor.Models;
using MachineMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MachineMonitor.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    private readonly ILogService _logService;

    public ObservableCollection<MachineLogEntry> Entries => _logService.Entries;

    public LogViewModel(ILogService logService)
    {
        _logService = logService;
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        var file = await PickSaveFile(
            "Exportar Eventos para CSV",
            $"MachineMonitor_Eventos_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        if (file is not null)
            await _logService.ExportToCsvAsync(file.Path.LocalPath);
    }

    [RelayCommand]
    private async Task ExportReadingsToCsvAsync()
    {
        var file = await PickSaveFile(
            "Exportar Leituras de Sensores para CSV",
            $"MachineMonitor_Leituras_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        if (file is not null)
            await _logService.ExportReadingsToCsvAsync(file.Path.LocalPath);
    }

    [RelayCommand]
    private void ClearLogs() => _logService.Entries.Clear();

    private static async Task<IStorageFile?> PickSaveFile(string title, string suggestedName)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } win })
            return null;

        return await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title             = title,
            DefaultExtension  = "csv",
            SuggestedFileName = suggestedName,
            FileTypeChoices   = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
    }
}
