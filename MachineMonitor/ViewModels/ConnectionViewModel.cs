using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MachineMonitor.Models;
using MachineMonitor.Services;
using System.Globalization;
using System.Threading.Tasks;

namespace MachineMonitor.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly ModbusServiceProxy _proxy;
    private readonly ILogService        _logService;
    private readonly ISettingsService   _settingsService;

    [ObservableProperty] private string _host;
    [ObservableProperty] private string _port;
    [ObservableProperty] private string _unitId;
    [ObservableProperty] private string _statusMessage = "Desconectado";
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private IBrush _statusIndicatorColor = Brushes.Gray;

    public string[] Modes { get; } = { "Simulado", "Modbus TCP" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimulatedMode))]
    private string _selectedMode;

    public bool IsSimulatedMode => SelectedMode == "Simulado";

    // ── Limiares de alarme (editáveis, aplicados em tempo real via ISettingsService.Current) ──
    [ObservableProperty] private string _criticalTempHigh;
    [ObservableProperty] private string _criticalPressHigh;
    [ObservableProperty] private string _criticalPressLow;
    [ObservableProperty] private string _criticalMotorLow;
    [ObservableProperty] private string _warnTempHigh;
    [ObservableProperty] private string _warnPressHigh;
    [ObservableProperty] private string _warnPressLow;
    [ObservableProperty] private string _warnMotorLow;
    [ObservableProperty] private string _thresholdsMessage = "";
    [ObservableProperty] private bool   _showThresholds;

    [RelayCommand]
    private void ToggleThresholds() => ShowThresholds = !ShowThresholds;

    partial void OnSelectedModeChanged(string value)
    {
        _proxy.SetMode(value == "Modbus TCP" ? ConnectionMode.ModbusTcp : ConnectionMode.Simulated);
        StatusMessage = value == "Simulado" ? "Desconectado (simulado)" : "Desconectado";
        StatusIndicatorColor = Brushes.Gray;
    }

    public ConnectionViewModel(ModbusServiceProxy proxy, ILogService logService, ISettingsService settingsService)
    {
        _proxy           = proxy;
        _logService      = logService;
        _settingsService = settingsService;

        // Carrega última configuração salva
        var s = settingsService.Load();
        _host         = s.Host;
        _port         = s.Port;
        _unitId       = s.UnitId;
        _selectedMode = s.Mode;
        _proxy.SetMode(s.Mode == "Modbus TCP" ? ConnectionMode.ModbusTcp : ConnectionMode.Simulated);

        _criticalTempHigh  = s.CriticalTempHigh.ToString(CultureInfo.InvariantCulture);
        _criticalPressHigh = s.CriticalPressHigh.ToString(CultureInfo.InvariantCulture);
        _criticalPressLow  = s.CriticalPressLow.ToString(CultureInfo.InvariantCulture);
        _criticalMotorLow  = s.CriticalMotorLow.ToString(CultureInfo.InvariantCulture);
        _warnTempHigh      = s.WarnTempHigh.ToString(CultureInfo.InvariantCulture);
        _warnPressHigh     = s.WarnPressHigh.ToString(CultureInfo.InvariantCulture);
        _warnPressLow      = s.WarnPressLow.ToString(CultureInfo.InvariantCulture);
        _warnMotorLow      = s.WarnMotorLow.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void SaveThresholds()
    {
        if (!TryParseDouble(CriticalTempHigh, out double criticalTempHigh) ||
            !TryParseDouble(CriticalPressHigh, out double criticalPressHigh) ||
            !TryParseDouble(CriticalPressLow, out double criticalPressLow) ||
            !TryParseDouble(CriticalMotorLow, out double criticalMotorLow) ||
            !TryParseDouble(WarnTempHigh, out double warnTempHigh) ||
            !TryParseDouble(WarnPressHigh, out double warnPressHigh) ||
            !TryParseDouble(WarnPressLow, out double warnPressLow) ||
            !TryParseDouble(WarnMotorLow, out double warnMotorLow))
        {
            ThresholdsMessage = "Valor inválido — use números (ex.: 95 ou 1,5).";
            return;
        }

        var current = _settingsService.Current;
        _settingsService.Save(new AppSettings
        {
            Host   = current.Host,
            Port   = current.Port,
            UnitId = current.UnitId,
            Mode   = current.Mode,

            CriticalTempHigh  = criticalTempHigh,
            CriticalPressHigh = criticalPressHigh,
            CriticalPressLow  = criticalPressLow,
            CriticalMotorLow  = criticalMotorLow,
            WarnTempHigh      = warnTempHigh,
            WarnPressHigh     = warnPressHigh,
            WarnPressLow      = warnPressLow,
            WarnMotorLow      = warnMotorLow,
        });

        ThresholdsMessage = "Limiares salvos — aplicados imediatamente, sem precisar reconectar.";
    }

    private static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

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

            // Persiste configuração usada com sucesso (preserva os limiares já configurados)
            var thresholds = _settingsService.Current;
            _settingsService.Save(new AppSettings
            {
                Host   = Host,
                Port   = Port,
                UnitId = UnitId,
                Mode   = SelectedMode,

                CriticalTempHigh  = thresholds.CriticalTempHigh,
                CriticalPressHigh = thresholds.CriticalPressHigh,
                CriticalPressLow  = thresholds.CriticalPressLow,
                CriticalMotorLow  = thresholds.CriticalMotorLow,
                WarnTempHigh      = thresholds.WarnTempHigh,
                WarnPressHigh     = thresholds.WarnPressHigh,
                WarnPressLow      = thresholds.WarnPressLow,
                WarnMotorLow      = thresholds.WarnMotorLow,
            });
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

    private bool CanConnect()    => !IsConnected && !IsBusy;
    private bool CanDisconnect() =>  IsConnected && !IsBusy;

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
