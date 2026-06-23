namespace MachineMonitor.Models;

/// <summary>
/// Mapa central de endereços Modbus do equipamento.
///
/// Convenção de endereçamento Modbus clássico:
///   Coils           00001–09999  → leitura e escrita de bits individuais
///   Discrete Inputs 10001–19999  → leitura de bits (somente leitura)
///   Input Registers 30001–39999  → leitura de registradores 16 bits (somente leitura)
///   Holding Regs    40001–49999  → leitura e escrita de registradores 16 bits
///
/// Os valores aqui são índices base-0 usados nas funções Modbus:
///   FC01 ReadCoils / FC05 WriteSingleCoil         → Coils
///   FC02 ReadDiscreteInputs                       → Discrete Inputs
///   FC04 ReadInputRegisters                       → Input Registers
///   FC03 ReadHoldingRegisters / FC06 WriteRegister → Holding Registers
/// </summary>
public static class ModbusAddressMap
{
    // ─── Coils (FC01 / FC05) ─────────────────────────────────────────────────
    // Índice 0 = endereço Modbus clássico 00001
    // Índice 1 = endereço Modbus clássico 00002
    public static class Coils
    {
        public const int MachinePower = 0; // 00001 — liga/desliga a máquina
        public const int ResetAlarm   = 1; // 00002 — reset de alarme ativo
    }

    // ─── Discrete Inputs (FC02) ──────────────────────────────────────────────
    // Índice 0 = endereço Modbus clássico 10001
    // Índice 1 = endereço Modbus clássico 10002
    public static class DiscreteInputs
    {
        public const int EmergencyActive = 0; // 10001 — emergência acionada
        public const int MachineRunning  = 1; // 10002 — máquina em movimento
    }

    // ─── Input Registers (FC04) ──────────────────────────────────────────────
    // Índice 0 = endereço Modbus clássico 30001
    // Cada índice adicional +1 = +1 no endereço clássico (30002, 30003…)
    public static class InputRegisters
    {
        public const int Temperature      = 0; // 30001 — temperatura (°C × 10)
        public const int Pressure         = 1; // 30002 — pressão (bar × 100)
        public const int MotorSpeed       = 2; // 30003 — velocidade do motor (RPM)
        public const int ProductionCounter = 3; // 30004 — contador de produção
    }

    // ─── Holding Registers (FC03 / FC06) ─────────────────────────────────────
    // Índice 0 = endereço Modbus clássico 40001
    public static class HoldingRegisters
    {
        public const int TemperatureSetpoint = 0; // 40001 — setpoint de temperatura (°C × 10)
        public const int MotorSpeedSetpoint  = 1; // 40002 — setpoint de velocidade (RPM)
    }
}
