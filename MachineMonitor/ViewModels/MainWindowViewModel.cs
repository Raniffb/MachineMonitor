using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;

namespace MachineMonitor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentView;
    [ObservableProperty] private string _pageTitle = "Configurar Conexão";

    // ── Status sidebar ───────────────────────────────────────────────────────
    [ObservableProperty] private IBrush _connectionStatusColor = new SolidColorBrush(Color.Parse("#4A5568"));
    [ObservableProperty] private string _connectionStatusLabel = "Desconectado";
    [ObservableProperty] private IBrush _machineStatusColor = new SolidColorBrush(Color.Parse("#4A5568"));
    [ObservableProperty] private string _machineStatusLabel = "---";
    [ObservableProperty] private IBrush _alarmStatusColor = new SolidColorBrush(Color.Parse("#4A5568"));
    [ObservableProperty] private string _alarmStatusLabel = "---";

    // ── Estado de navegação ──────────────────────────────────────────────────
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isOnDashboard;
    [ObservableProperty] private bool _isOnLogs;

    public ConnectionViewModel ConnectionViewModel { get; }
    public DashboardViewModel DashboardViewModel { get; }
    public LogViewModel LogViewModel { get; }

    public MainWindowViewModel(
        ConnectionViewModel connectionViewModel,
        DashboardViewModel dashboardViewModel,
        LogViewModel logViewModel)
    {
        ConnectionViewModel = connectionViewModel;
        DashboardViewModel  = dashboardViewModel;
        LogViewModel        = logViewModel;
        _currentView        = connectionViewModel;

        ConnectionViewModel.PropertyChanged += OnConnectionChanged;
        DashboardViewModel.PropertyChanged  += OnDashboardChanged;
    }

    // ── Comandos de navegação ─────────────────────────────────────────────────

    [RelayCommand]
    private void ShowDashboard()
    {
        CurrentView    = DashboardViewModel;
        PageTitle      = "Dashboard";
        IsOnDashboard  = true;
        IsOnLogs       = false;
    }

    [RelayCommand]
    private void ShowLogs()
    {
        CurrentView   = LogViewModel;
        PageTitle     = "Histórico de Eventos";
        IsOnDashboard = false;
        IsOnLogs      = true;
    }

    // ── Reação a eventos de conexão ──────────────────────────────────────────

    private void OnConnectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionViewModel.IsBusy) && ConnectionViewModel.IsBusy)
        {
            ConnectionStatusColor = new SolidColorBrush(Color.Parse("#FFB800"));
            ConnectionStatusLabel = "Conectando...";
            return;
        }

        if (e.PropertyName != nameof(ConnectionViewModel.IsConnected)) return;

        if (ConnectionViewModel.IsConnected)
        {
            IsConnected = true;
            ShowDashboard();
            DashboardViewModel.StartPolling();
            ConnectionStatusColor = new SolidColorBrush(Color.Parse("#00C853"));
            ConnectionStatusLabel = "Conectado";
            MachineStatusColor    = new SolidColorBrush(Color.Parse("#4A5568"));
            MachineStatusLabel    = "Aguardando...";
            AlarmStatusColor      = new SolidColorBrush(Color.Parse("#4A5568"));
            AlarmStatusLabel      = "---";
        }
        else
        {
            IsConnected = false;
            DashboardViewModel.StopPolling();
            CurrentView           = ConnectionViewModel;
            PageTitle             = "Configurar Conexão";
            IsOnDashboard         = false;
            IsOnLogs              = false;
            ConnectionStatusColor = new SolidColorBrush(Color.Parse("#4A5568"));
            ConnectionStatusLabel = "Desconectado";
            MachineStatusColor    = new SolidColorBrush(Color.Parse("#4A5568"));
            MachineStatusLabel    = "---";
            AlarmStatusColor      = new SolidColorBrush(Color.Parse("#4A5568"));
            AlarmStatusLabel      = "---";
        }
    }

    private void OnDashboardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.MachineOn))
        {
            MachineStatusColor = DashboardViewModel.MachineOn
                ? new SolidColorBrush(Color.Parse("#00C853"))
                : new SolidColorBrush(Color.Parse("#4A5568"));
            MachineStatusLabel = DashboardViewModel.MachineOn ? "Ligada" : "Desligada";
        }
        else if (e.PropertyName == nameof(DashboardViewModel.AlarmActive))
        {
            AlarmStatusColor = DashboardViewModel.AlarmActive
                ? new SolidColorBrush(Color.Parse("#FF3D3D"))
                : new SolidColorBrush(Color.Parse("#00C853"));
            AlarmStatusLabel = DashboardViewModel.AlarmActive ? "ATIVO!" : "Normal";
        }
    }
}
