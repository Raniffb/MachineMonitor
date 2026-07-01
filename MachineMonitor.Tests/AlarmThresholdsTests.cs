using MachineMonitor.Services;

namespace MachineMonitor.Tests;

public class AlarmThresholdsTests
{
    // ── Sem condição — nada dispara ─────────────────────────────────────────
    [Fact]
    public void InferCriticalReason_NormalValues_ReturnsEmpty()
    {
        string reason = AlarmThresholds.InferCriticalReason(temperature: 75, pressure: 4.5, machineOn: true, motorSpeed: 1450);
        Assert.Equal("", reason);
    }

    [Fact]
    public void InferWarningReason_NormalValues_ReturnsEmpty()
    {
        string reason = AlarmThresholds.InferWarningReason(temperature: 75, pressure: 4.5, machineOn: true, motorSpeed: 1450);
        Assert.Equal("", reason);
    }

    // ── Limiares críticos ────────────────────────────────────────────────────
    [Theory]
    [InlineData(96, 4.5, true, 1450, "Superaquecimento")]
    [InlineData(75, 7.1, true, 1450, "Sobrepressão")]
    [InlineData(75, 0.9, true, 1450, "Baixa pressão")]
    [InlineData(75, 4.5, true, 199, "Falha de partida")]
    public void InferCriticalReason_ConditionBreached_ReturnsExpectedReason(
        double temperature, double pressure, bool machineOn, double motorSpeed, string expectedSubstring)
    {
        string reason = AlarmThresholds.InferCriticalReason(temperature, pressure, machineOn, motorSpeed);
        Assert.Contains(expectedSubstring, reason);
    }

    [Fact]
    public void InferCriticalReason_MotorStall_IgnoredWhenMachineOff()
    {
        string reason = AlarmThresholds.InferCriticalReason(temperature: 75, pressure: 4.5, machineOn: false, motorSpeed: 0);
        Assert.Equal("", reason);
    }

    [Theory]
    [InlineData(95, 4.5, true, 1450)]  // exatamente no limiar não deve disparar (operador é '>')
    [InlineData(75, 7.0, true, 1450)]
    [InlineData(75, 1.0, true, 1450)]
    [InlineData(75, 4.5, true, 200)]
    public void InferCriticalReason_ExactlyAtThreshold_DoesNotTrigger(
        double temperature, double pressure, bool machineOn, double motorSpeed)
    {
        string reason = AlarmThresholds.InferCriticalReason(temperature, pressure, machineOn, motorSpeed);
        Assert.Equal("", reason);
    }

    // ── Limiares de aviso ────────────────────────────────────────────────────
    [Theory]
    [InlineData(86, 4.5, true, 1450, "Temp. elevada")]
    [InlineData(75, 6.1, true, 1450, "Pressão alta")]
    [InlineData(75, 1.4, true, 1450, "Pressão baixa")]
    [InlineData(75, 4.5, true, 299, "Velocidade baixa")]
    public void InferWarningReason_ConditionBreached_ReturnsExpectedReason(
        double temperature, double pressure, bool machineOn, double motorSpeed, string expectedSubstring)
    {
        string reason = AlarmThresholds.InferWarningReason(temperature, pressure, machineOn, motorSpeed);
        Assert.Contains(expectedSubstring, reason);
    }

    // ── Prioridade: verifica ordem de checagem (temperatura antes de pressão) ─
    [Fact]
    public void InferCriticalReason_MultipleConditionsBreached_PrioritizesTemperature()
    {
        string reason = AlarmThresholds.InferCriticalReason(temperature: 100, pressure: 8.0, machineOn: true, motorSpeed: 100);
        Assert.Contains("Superaquecimento", reason);
    }
}
