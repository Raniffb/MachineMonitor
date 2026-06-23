using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MachineMonitor.Services;
using MachineMonitor.ViewModels;
using MachineMonitor.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MachineMonitor;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Serviços de infraestrutura
        services.AddSingleton<ILogService, LogService>();

        // Serviços Modbus
        services.AddSingleton<FakeModbusService>();
        services.AddSingleton<NModbusService>();
        services.AddSingleton<ModbusServiceProxy>();
        services.AddSingleton<IModbusService>(sp => sp.GetRequiredService<ModbusServiceProxy>());

        // ViewModels — DI injeta automaticamente todos os parâmetros dos construtores
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
