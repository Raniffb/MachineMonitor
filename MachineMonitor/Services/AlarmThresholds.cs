using MachineMonitor.Models;

namespace MachineMonitor.Services;

// Regras de alarme compartilhadas entre FakeModbusService e NModbusService,
// para evitar divergência entre a simulação interna e o servidor Modbus TCP.
// Os limiares em si vêm de AppSettings (configuráveis pela tela de conexão).
internal static class AlarmThresholds
{
    public static string InferCriticalReason(double temperature, double pressure, bool machineOn, double motorSpeed, AppSettings settings)
    {
        if      (temperature > settings.CriticalTempHigh)
            return $"Superaquecimento ({temperature:F1} °C > {settings.CriticalTempHigh:F0} °C)";
        else if (pressure > settings.CriticalPressHigh)
            return $"Sobrepressão ({pressure:F2} bar > {settings.CriticalPressHigh:F1} bar)";
        else if (pressure < settings.CriticalPressLow)
            return $"Baixa pressão ({pressure:F2} bar < {settings.CriticalPressLow:F1} bar)";
        else if (machineOn && motorSpeed < settings.CriticalMotorLow)
            return $"Falha de partida ({motorSpeed:F0} rpm < {settings.CriticalMotorLow:F0} rpm)";
        return "";
    }

    public static string InferWarningReason(double temperature, double pressure, bool machineOn, double motorSpeed, AppSettings settings)
    {
        if      (temperature > settings.WarnTempHigh)
            return $"Temp. elevada ({temperature:F1} °C > {settings.WarnTempHigh:F0} °C)";
        else if (pressure > settings.WarnPressHigh)
            return $"Pressão alta ({pressure:F2} bar > {settings.WarnPressHigh:F1} bar)";
        else if (pressure < settings.WarnPressLow)
            return $"Pressão baixa ({pressure:F2} bar < {settings.WarnPressLow:F1} bar)";
        else if (machineOn && motorSpeed < settings.WarnMotorLow)
            return $"Velocidade baixa ({motorSpeed:F0} rpm < {settings.WarnMotorLow:F0} rpm)";
        return "";
    }
}
