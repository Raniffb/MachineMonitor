using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MachineMonitor.Models;
using MachineMonitor.Services;
using System.Threading.Tasks;

namespace MachineMonitor.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly ModbusServiceProxy _proxy;
    private readonly ILogService _logService;

    [ObservableProperty] private string _host = "127.0.0.1";
    [ObservableProperty] private string _port = "5020";
    [ObservableProperty] private string _unitId = "1";
    [ObservableProperty] private string _statusMessage = "Desconectado";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private IBrush _statusIndicatorColor = Brushes.Gray;

    // ── Seleção de modo ───────────────────────────────────────────────────────
    public string[] Modes { get; } = { "Simulado", "Modbus TCP" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimulatedMode))]
    private string _selectedMode = "Simulado";

    public bool IsSimulatedMode => SelectedMode == "Simulado";

    partial void OnSelectedModeChanged(string value)
    {
        _proxy.SetMode(value == "Modbus TCP" ? ConnectionMode.ModbusTcp : ConnectionMode.Simulated);
        StatusMessage = value == "Simulado" ? "Desconectado (simulado)" : "Desconectado";
        StatusIndicatorColor = Brushes.Gray;
    }

    public ConnectionViewModel(ModbusServiceProxy proxy, ILogService logService)
    {
        _proxy = proxy;
        _logService = logService;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        StatusMessage = "Conectando...";
        StatusIndicatorColor = Brushes.Yellow;

        if (!int.TryParse(Port, out int port) || !byte.TryParse(UnitId, out byte unit))
        {
            StatusMessage = "Porta ou Unit ID inválidos.";
            StatusIndicatorColor = Brushes.OrangeRed;
            IsBusy = false;
            return;
        }

        bool result = await _proxy.ConnectAsync(Host, port, unit);
        IsConnected = result;

        if (result)
        {
            string desc = SelectedMode == "Simulado" ? "modo simulado" : $"{Host}:{port}";
            StatusMessage = SelectedMode == "Simulado"
                ? "Conectado (simulado)"
                : $"Conectado a {Host}:{port}";
            StatusIndicatorColor = new SolidColorBrush(Color.Parse("#00C853"));
            _logService.Add(LogEventType.Connected, $"Conectado — {desc}  (Unit ID {unit})");
        }
        else
        {
            StatusMessage = "Falha na conexão";
            StatusIndicatorColor = new SolidColorBrush(Color.Parse("#FF5252"));
            _logService.Add(LogEventType.CommError, $"Falha ao conectar em {Host}:{port}.");
        }

        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        StatusMessage = "Desconectando...";
        StatusIndicatorColor = Brushes.Yellow;
        await _proxy.DisconnectAsync();
        IsConnected = false;
        StatusMessage = "Desconectado";
        StatusIndicatorColor = Brushes.Gray;
        IsBusy = false;
        _logService.Add(LogEventType.Disconnected, "Conexão encerrada pelo operador.");
    }

    private bool CanConnect() => !IsConnected && !IsBusy;
    private bool CanDisconnect() => IsConnected && !IsBusy;

    partial void OnIsConnectedChanged(bool value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }
}
