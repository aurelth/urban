import { MonthReadings } from '../../core/finance/finance.models';
import { currentMonth, isMonth, isMonthInWindow } from '../../core/finance/month';

/** O gás aparece quando a tarifa em vigor o cobra, ou quando já há leitura de gás gravada que não deve ficar escondida. */
export function showsGas(data: MonthReadings): boolean {
  return data.hasGas || data.units.some((unit) => unit.gas !== null);
}

/** "Água: 2 de 3 unidades com leitura", e o mesmo para o gás quando o mês o mostra. */
export function coverageLines(data: MonthReadings): readonly string[] {
  const total = data.units.length;
  const line = (label: string, filled: number): string =>
    `${label}: ${String(filled)} de ${String(total)} ${total === 1 ? 'unidade' : 'unidades'} com leitura`;
  return [
    line('Água', data.units.filter((unit) => unit.water !== null).length),
    ...(showsGas(data) ? [line('Gás', data.units.filter((unit) => unit.gas !== null).length)] : []),
  ];
}

/** O mês pedido na barra de endereços (`?mes=AAAA-MM`), se for um mês válido dentro dos 5 anos da tela; senão, o de hoje. */
export function initialMonth(requested: string | null, now: Date): string {
  return requested !== null && isMonth(requested) && isMonthInWindow(requested, now)
    ? requested
    : currentMonth(now);
}
