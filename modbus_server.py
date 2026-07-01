#!/usr/bin/env python3
"""
Servidor Modbus TCP simulado para o MachineMonitor.

Instale : pip install "pymodbus==3.6.9"
Execute : python modbus_server.py

Configuração no MachineMonitor:
  Modo    -> Modbus TCP
  Host    -> 127.0.0.1
  Porta   -> 5020
  Unit ID -> 1
"""

import asyncio
import random
import sys
import time
from datetime import datetime

from pymodbus.datastore import (
    ModbusSequentialDataBlock,
    ModbusServerContext,
    ModbusSlaveContext,
)
from pymodbus.server import StartAsyncTcpServer

# ── Configuração ──────────────────────────────────────────────────────────────
HOST = "0.0.0.0"
PORT = 5020     # Porta alta — sem necessidade de administrador no Windows

# ── Mapa de endereços (espelho do ModbusAddressMap.cs) ────────────────────────
COIL_MACHINE_POWER = 0   # FC05 endereço 0  (00001)
COIL_RESET_ALARM   = 1   # FC05 endereço 1  (00002)

DI_EMERGENCY       = 0   # FC02 endereço 0  (10001)
DI_MACHINE_RUNNING = 1   # FC02 endereço 1  (10002)

IR_TEMPERATURE     = 0   # FC04 endereço 0  (30001)  unidade: °C × 10
IR_PRESSURE        = 1   # FC04 endereço 1  (30002)  unidade: bar × 100
IR_MOTOR_SPEED     = 2   # FC04 endereço 2  (30003)  unidade: RPM
IR_PRODUCTION      = 3   # FC04 endereço 3  (30004)

HR_TEMP_SETPOINT   = 0   # FC06 endereço 0  (40001)  unidade: °C × 10
HR_MOTOR_SETPOINT  = 1   # FC06 endereço 1  (40002)  unidade: RPM

# ── Estado interno do simulador ───────────────────────────────────────────────
production_count       = 0
alarm_latched          = False   # latchado: só limpa via ResetAlarm
alarm_suppressed_until = 0.0
last_alarm_reason      = ""

# ── Limiares de alarme (espelham FakeModbusService.cs) ───────────────────────
ALARM_TEMP_HIGH  =  95.0   # °C
ALARM_PRESS_HIGH =   7.0   # bar
ALARM_PRESS_LOW  =   1.0   # bar
ALARM_MOTOR_LOW  = 200     # rpm


def build_context():
    """Cria o ModbusServerContext com blocos de dados iniciais."""
    pad = 10  # registradores extras de buffer
    store = ModbusSlaveContext(
        co=ModbusSequentialDataBlock(0, [False] * (2 + pad)),
        di=ModbusSequentialDataBlock(0, [False] * (2 + pad)),
        hr=ModbusSequentialDataBlock(0, [0]     * (2 + pad)),
        ir=ModbusSequentialDataBlock(0, [0]     * (4 + pad)),
    )
    # Inicializa com setValues (endereçamento 0-based confirmado)
    store.setValues(1, COIL_MACHINE_POWER, [True])    # Máquina ligada por padrão
    store.setValues(2, DI_MACHINE_RUNNING, [True])    # Running=True
    store.setValues(3, HR_TEMP_SETPOINT,   [750])     # setpoint 75.0 °C
    store.setValues(3, HR_MOTOR_SETPOINT,  [1450])    # setpoint 1450 RPM
    store.setValues(4, IR_TEMPERATURE,     [750])     # 75.0 °C (× 10)
    store.setValues(4, IR_PRESSURE,        [465])     # 4.65 bar (× 100)
    store.setValues(4, IR_MOTOR_SPEED,     [1450])    # 1450 RPM
    store.setValues(4, IR_PRODUCTION,      [0])

    return ModbusServerContext(slaves=store, single=True), store


async def update_loop(store):
    """Atualiza registradores a cada 1 s simulando dinâmica de máquina real."""
    global production_count, alarm_latched, alarm_suppressed_until, last_alarm_reason

    while True:
        await asyncio.sleep(1)
        try:
            # ── Lê Coils escritos pelo cliente (FC05) ────────────────────────
            coils       = store.getValues(1, COIL_MACHINE_POWER, count=2)
            machine_on  = bool(coils[0])
            reset_alarm = bool(coils[1]) if len(coils) > 1 else False

            # Pulso de reset: limpa latch, suprime novos disparos por 10 s
            if reset_alarm:
                alarm_latched          = False
                alarm_suppressed_until = time.time() + 3
                store.setValues(1, COIL_RESET_ALARM, [False])

            # ── Lê setpoints dos Holding Registers (FC06) ────────────────────
            hr       = store.getValues(3, HR_TEMP_SETPOINT, count=2)
            temp_sp  = (hr[0] / 10.0) if hr[0] else 75.0   # °C × 10 → °C
            motor_sp = hr[1]           if hr[1] else 1450   # RPM direto

            # ── Simula valores com oscilação realista ─────────────────────────
            temperature = round(temp_sp  + random.uniform(-5.0,   5.0), 1)
            pressure    = round(4.5      + random.uniform( 0.0,   1.5), 2)
            motor_speed = (
                max(0, int(round(motor_sp + random.uniform(-100, 100))))
                if machine_on else 0
            )

            if machine_on:
                production_count += 1

            # Perturbação de processo: 2% de chance por ciclo (≈1 vez a cada 50 s).
            # Provoca um pico que viola uma das condições de alarme.
            if not alarm_latched and time.time() > alarm_suppressed_until:
                if random.random() < 0.001:
                    upset = random.randint(0, 3)
                    if upset == 0:
                        temperature = round(temperature + 20.0 + random.uniform(0, 15), 1)
                    elif upset == 1:
                        pressure = round(pressure + 3.0 + random.uniform(0, 2), 2)
                    elif upset == 2:
                        pressure = round(pressure - 3.5 - random.uniform(0, 1), 2)
                    elif upset == 3 and machine_on:
                        motor_speed = random.randint(0, 150)  # stall

            # Verifica condições somente fora do período de supressão pós-reset
            if not alarm_latched and time.time() > alarm_suppressed_until:
                reason = ""
                if temperature > ALARM_TEMP_HIGH:
                    reason = f"Superaquecimento ({temperature:.1f} C > {ALARM_TEMP_HIGH} C)"
                elif pressure > ALARM_PRESS_HIGH:
                    reason = f"Sobrepressao ({pressure:.2f} bar > {ALARM_PRESS_HIGH} bar)"
                elif pressure < ALARM_PRESS_LOW:
                    reason = f"Baixa pressao ({pressure:.2f} bar < {ALARM_PRESS_LOW} bar)"
                elif machine_on and motor_speed < ALARM_MOTOR_LOW:
                    reason = f"Falha de partida ({motor_speed} rpm < {ALARM_MOTOR_LOW} rpm)"
                if reason:
                    alarm_latched     = True
                    last_alarm_reason = reason
                    store.setValues(1, COIL_MACHINE_POWER, [False])   # safe state

            alarm = alarm_latched

            # ── Escreve Input Registers (FC04) — lidos pelo MachineMonitor ───
            store.setValues(4, IR_TEMPERATURE, [int(temperature * 10)])
            store.setValues(4, IR_PRESSURE,    [int(pressure    * 100)])
            store.setValues(4, IR_MOTOR_SPEED, [motor_speed])
            store.setValues(4, IR_PRODUCTION,  [production_count])

            # ── Escreve Discrete Inputs (FC02) ───────────────────────────────
            store.setValues(2, DI_EMERGENCY,       [alarm])
            store.setValues(2, DI_MACHINE_RUNNING, [machine_on])

            # ── Console ───────────────────────────────────────────────────────
            ts        = datetime.now().strftime("%H:%M:%S")
            status    = "ON " if machine_on else "OFF"
            alarm_str = f"ALARM({last_alarm_reason})" if alarm else "OK"
            print(
                f"\r[{ts}]  "
                f"Temp:{temperature:5.1f}C  "
                f"P:{pressure:4.2f}bar  "
                f"Motor:{motor_speed:5d}rpm  "
                f"Maq:{status}  "
                f"Prod:{production_count:4d}  "
                f"Alarme:{alarm_str}    ",
                end="",
                flush=True,
            )

        except Exception as exc:
            print(f"\n[loop] Erro: {exc}")


async def main():
    print()
    print("=" * 68)
    print("  Servidor Modbus TCP  —  MachineMonitor Simulator")
    print(f"  Porta : {PORT}   Unit ID : 1   Host : {HOST}")
    print("=" * 68)
    print()
    print("  Configure o MachineMonitor assim:")
    print("    Modo    ->  Modbus TCP")
    print("    Host    ->  127.0.0.1")
    print(f"    Porta   ->  {PORT}")
    print("    Unit ID ->  1")
    print()
    print("  Comandos suportados:")
    print("    Ligar / Desligar  ->  FC05  Coil 0")
    print("    Reset Alarme      ->  FC05  Coil 1")
    print("    Setpoints         ->  FC06  HR 0 e 1")
    print()
    print("  Aguardando conexao... (Ctrl+C para encerrar)")
    print()

    context, store = build_context()

    # Inicia o loop de simulação em paralelo com o servidor
    asyncio.create_task(update_loop(store))

    await StartAsyncTcpServer(context, address=(HOST, PORT))


if __name__ == "__main__":
    # pymodbus 3.x precisa do SelectorEventLoop no Windows
    if sys.platform == "win32":
        asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n\nServidor encerrado.")
