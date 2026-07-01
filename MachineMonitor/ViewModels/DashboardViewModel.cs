using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MachineMonitor.Models;
using MachineMonitor.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MachineMonitor.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IModbusService _modbusService;
    private readonly ILogService    _logService;
    private CancellationTokenSource? _cts;

    // Persistência do alarme crítico e detecção de transição
    private bool   _previousAlarmActive;
    private string _persistedAlarmReason = "";

    // Retry automático: reconecta após N falhas consecutivas
    private int  _consecutiveErrors         = 0;
    private const int MaxErrorsBeforeRetry  = 3;
    private const int MaxReconnectAttempts  = 3;

    // ── Leituras do dashboard ────────────────────────────────────────────────
    [ObservableProperty] private double _temperature;
    [ObservableProperty] private double _pressure;
    [ObservableProperty] private double _motorSpeed;
    [ObservableProperty] private int    _productionCount;
    [ObservableProperty] private string _lastUpdate = "--";
    [ObservableProperty] private bool   _machineOn;
    [ObservableProperty] private bool   _alarmActive;
    [ObservableProperty] private bool   _warningActive;
    [ObservableProperty] private string _machineOnLabel = "DESLIGADA";
    [ObservableProperty] private IBrush _machineOnColor = Brushes.Gray;
    [ObservableProperty] private string _alarmLabel = "INATIVO";
    [ObservableProperty] private IBrush _alarmColor = Brushes.Gray;
    [ObservableProperty] private string _alarmReason = "";

    // ── Estado para habilitar/desabilitar comandos ───────────────────────────
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isCommandBusy;

    // ── Inputs de setpoint ───────────────────────────────────────────────────
    [ObservableProperty] private string _temperatureSetpointInput = "75";
    [ObservableProperty] private string _motorSpeedSetpointInput  = "1450";

    // ── Feedback de comandos ─────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommandMessage))]
    private string _commandMessage = "";

    [ObservableProperty] private IBrush _commandMessageColor = Brushes.Transparent;

    public bool HasCommandMessage => !string.IsNullOrEmpty(CommandMessage);

    public DashboardViewModel(IModbusService modbusService, ILogService logService)
    {
        _modbusService = modbusService;
        _logService    = logService;
    }

    // ── Polling ──────────────────────────────────────────────────────────────

    public void StartPolling()
    {
        StopPolling();
        IsConnected           = true;
        CommandMessage        = "";
        _previousAlarmActive  = false;
        _persistedAlarmReason = "";
        _consecutiveErrors    = 0;
        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);
    }

    public void StopPolling()
    {
        IsConnected = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var data = await _modbusService.ReadMachineDataAsync();

                if (data != null)
                {
                    _consecutiveErrors = 0;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Temperature     = data.Temperature;
                        Pressure        = data.Pressure;
                        MotorSpeed      = data.MotorSpeed;
                        ProductionCount = data.ProductionCount;
                        LastUpdate      = DateTime.Now.ToString("HH:mm:ss");

                        MachineOn      = data.MachineOn;
                        MachineOnLabel = data.MachineOn ? "LIGADA" : "DESLIGADA";
                        MachineOnColor = data.MachineOn
                            ? new SolidColorBrush(Color.Parse("#00C853"))
                            : new SolidColorBrush(Color.Parse("#555555"));

                        // Persiste razão do alarme crítico (pico dura 1 ciclo no TCP)
                        if (data.AlarmActive)
                        {
                            if (!string.IsNullOrEmpty(data.AlarmReason))
                                _persistedAlarmReason = data.AlarmReason;
                        }
                        else
                        {
                            _persistedAlarmReason = "";
                        }

                        // Prioridade: Crítico > Aviso > Inativo
                        if (data.AlarmActive)
                        {
                            AlarmActive = true;
                            WarningActive = false;
                            AlarmLabel  = "CRÍTICO";
                            AlarmColor  = new SolidColorBrush(Color.Parse("#FF5252"));
                            AlarmReason = _persistedAlarmReason;
                        }
                        else if (data.WarningActive)
                        {
                            AlarmActive   = false;
                            WarningActive = true;
                            AlarmLabel    = "AVISO";
                            AlarmColor    = new SolidColorBrush(Color.Parse("#FF9800"));
                            AlarmReason   = data.WarningReason;
                        }
                        else
                        {
                            AlarmActive   = false;
                            WarningActive = false;
                            AlarmLabel    = "INATIVO";
                            AlarmColor    = new SolidColorBrush(Color.Parse("#555555"));
                            AlarmReason   = "";
                        }

                        // Detecta transição false→true do alarme crítico para log
                        if (data.AlarmActive && !_previousAlarmActive)
                        {
                            string detail = string.IsNullOrEmpty(data.AlarmReason)
                                ? "Alarme disparado."
                                : data.AlarmReason;
                            _logService.Add(LogEventType.AlarmTriggered, detail);
                        }
                        _previousAlarmActive = data.AlarmActive;
                    });

                    // Registra leitura para gráfico e exportação
                    _logService.AddReading(new SensorReading
                    {
                        Timestamp     = DateTime.Now,
                        Temperature   = data.Temperature,
                        Pressure      = data.Pressure,
                        MotorSpeed    = data.MotorSpeed,
                        ProductionCount = data.ProductionCount,
                        MachineOn     = data.MachineOn,
                        AlarmActive   = data.AlarmActive,
                    });
                }
                else
                {
                    _consecutiveErrors++;
                    _logService.Add(LogEventType.CommError,
                        $"Falha de leitura ({_consecutiveErrors}/{MaxErrorsBeforeRetry}).");

                    if (_consecutiveErrors >= MaxErrorsBeforeRetry)
                    {
                        _consecutiveErrors = 0;
                        await TryReconnectAsync(ct);
                    }
                }

                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TryReconnectAsync(CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
        {
            if (ct.IsCancellationRequested) return;
            _logService.Add(LogEventType.CommError,
                $"Tentativa de reconexão {attempt}/{MaxReconnectAttempts}...");

            bool ok = await _modbusService.ReconnectAsync();
            if (ok)
            {
                _logService.Add(LogEventType.Connected, "Reconectado automaticamente.");
                return;
            }
            if (attempt < MaxReconnectAttempts)
                await Task.Delay(2000, ct);
        }

        _logService.Add(LogEventType.Disconnected,
            "Reconexão falhou após 3 tentativas. Verifique o dispositivo.");
        await Dispatcher.UIThread.InvokeAsync(StopPolling);
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    private bool CanSendCommand() => IsConnected && !IsCommandBusy;

    [RelayCommand(CanExecute = nameof(CanSendCommand))]
    private async Task TurnOnMachineAsync()
    {
        IsCommandBusy = true;
        try
        {
            bool ok = await _modbusService.TurnOnMachineAsync();
            SetFeedback(ok, "Máquina ligada com sucesso.", "Falha ao ligar a máquina.");
            if (ok) _logService.Add(LogEventType.MachineOn, "Comando: máquina ligada.");
        }
        finally { IsCommandBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSendCommand))]
    private async Task TurnOffMachineAsync()
    {
        IsCommandBusy = true;
        try
        {
            bool ok = await _modbusService.TurnOffMachineAsync();
            SetFeedback(ok, "Máquina desligada com sucesso.", "Falha ao desligar a máquina.");
            if (ok) _logService.Add(LogEventType.MachineOff, "Comando: máquina desligada.");
        }
        finally { IsCommandBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSendCommand))]
    private async Task ResetAlarmAsync()
    {
        IsCommandBusy = true;
        try
        {
            bool ok = await _modbusService.ResetAlarmAsync();
            SetFeedback(ok, "Alarme resetado. Suprimido por 3 s.", "Falha ao resetar alarme.");
            if (ok) _logService.Add(LogEventType.AlarmReset, "Alarme resetado pelo operador.");
        }
        finally { IsCommandBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSendCommand))]
    private async Task SendSetpointsAsync()
    {
        if (!double.TryParse(TemperatureSetpointInput, out double tempSp) ||
            !double.TryParse(MotorSpeedSetpointInput,  out double motorSp))
        {
            SetFeedback(false, "", "Valores de setpoint inválidos.");
            return;
        }

        IsCommandBusy = true;
        try
        {
            bool ok = await _modbusService.WriteSetpointsAsync(tempSp, motorSp);
            SetFeedback(ok,
                $"Setpoints enviados: Temp = {tempSp:F1} °C | Motor = {motorSp:F0} rpm",
                "Falha ao enviar setpoints.");
            if (ok)
                _logService.Add(LogEventType.SetpointChanged,
                    $"Setpoints alterados: Temp = {tempSp:F1} °C | Motor = {motorSp:F0} rpm");
        }
        finally { IsCommandBusy = false; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetFeedback(bool success, string okMessage, string errMessage)
    {
        CommandMessage      = success ? $"✓  {okMessage}" : $"✗  {errMessage}";
        CommandMessageColor = success
            ? new SolidColorBrush(Color.Parse("#00C853"))
            : new SolidColorBrush(Color.Parse("#FF5252"));
    }

    partial void OnIsConnectedChanged(bool value)   => NotifyCommandsCanExecuteChanged();
    partial void OnIsCommandBusyChanged(bool value) => NotifyCommandsCanExecuteChanged();

    private void NotifyCommandsCanExecuteChanged()
    {
        TurnOnMachineCommand.NotifyCanExecuteChanged();
        TurnOffMachineCommand.NotifyCanExecuteChanged();
        ResetAlarmCommand.NotifyCanExecuteChanged();
        SendSetpointsCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        StopPolling();
        GC.SuppressFinalize(this);
    }
}
