namespace MachineMonitor.Services;

// Limiares e regras de alarme compartilhados entre FakeModbusService e NModbusService,
// para evitar divergência entre a simulação interna e o servidor Modbus TCP.
internal static class AlarmThresholds
{
    // ── Limiares críticos (disparam alarme latchado) ──────────────────────────
    public const double CriticalTempHigh  =  95.0;
    public const double CriticalPressHigh =   7.0;
    public const double CriticalPressLow  =   1.0;
    public const double CriticalMotorLow  = 200.0;

    // ── Limiares de aviso (auto-clearing, abaixo do crítico) ─────────────────
    public const double WarnTempHigh  =  85.0;
    public const double WarnPressHigh =   6.0;
    public const double WarnPressLow  =   1.5;
    public const double WarnMotorLow  = 300.0;

    public static string InferCriticalReason(double temperature, double pressure, bool machineOn, double motorSpeed)
    {
        if      (temperature > CriticalTempHigh)
            return $"Superaquecimento ({temperature:F1} °C > {CriticalTempHigh} °C)";
        else if (pressure > CriticalPressHigh)
            return $"Sobrepressão ({pressure:F2} bar > {CriticalPressHigh:F1} bar)";
        else if (pressure < CriticalPressLow)
            return $"Baixa pressão ({pressure:F2} bar < {CriticalPressLow:F1} bar)";
        else if (machineOn && motorSpeed < CriticalMotorLow)
            return $"Falha de partida ({motorSpeed:F0} rpm < {CriticalMotorLow:F0} rpm)";
        return "";
    }

    public static string InferWarningReason(double temperature, double pressure, bool machineOn, double motorSpeed)
    {
        if      (temperature > WarnTempHigh)
            return $"Temp. elevada ({temperature:F1} °C > {WarnTempHigh} °C)";
        else if (pressure > WarnPressHigh)
            return $"Pressão alta ({pressure:F2} bar > {WarnPressHigh:F1} bar)";
        else if (pressure < WarnPressLow)
            return $"Pressão baixa ({pressure:F2} bar < {WarnPressLow:F1} bar)";
        else if (machineOn && motorSpeed < WarnMotorLow)
            return $"Velocidade baixa ({motorSpeed:F0} rpm < {WarnMotorLow:F0} rpm)";
        return "";
    }
}
