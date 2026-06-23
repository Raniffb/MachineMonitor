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
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } win })
            return;

        var file = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title             = "Exportar Logs para CSV",
            DefaultExtension  = "csv",
            SuggestedFileName = $"MachineMonitor_Log_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            FileTypeChoices   = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });

        if (file is not null)
            await _logService.ExportToCsvAsync(file.Path.LocalPath);
    }

    [RelayCommand]
    private void ClearLogs() => _logService.Entries.Clear();
}
