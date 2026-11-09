import { ConsumptionTariff } from '../../core/finance/finance.models';
import { formatDecimalInput } from '../../core/finance/decimal';
import { formatMoney } from '../../core/finance/money';
import { monthTitle } from '../../core/finance/month';

export interface TariffLine {
  readonly label: string;
  readonly text: string;
}

const NOT_CHARGED = 'Não cobrado';

/** "novembro de 2026": o mês no meio de uma frase. */
export function monthName(month: string): string {
  return monthTitle(month).toLowerCase();
}

/** "1 faixa" ou "3 faixas". */
export function bandsText(count: number): string {
  return `${String(count)} ${count === 1 ? 'faixa' : 'faixas'}`;
}

/** O que a tarifa cobra de água, esgoto e gás, uma linha por serviço. */
export function tariffLines(tariff: ConsumptionTariff): TariffLine[] {
  return [
    { label: 'Água', text: bandsText(tariff.water.length) },
    {
      label: 'Esgoto',
      text:
        tariff.sewage === null
          ? NOT_CHARGED
          : `${bandsText(tariff.sewage.length)}, cobrando ${formatDecimalInput(tariff.sewagePercent)}% do valor`,
    },
    {
      label: 'Gás',
      text:
        tariff.gas === null
          ? NOT_CHARGED
          : `${formatDecimalInput(tariff.gas.kilosPerCubicMeter)} kg/m³ × ${formatMoney(tariff.gas.pricePerKilo)} por kg = ${formatMoney(tariff.gas.pricePerCubicMeter)} por m³`,
    },
  ];
}

export function reserveFundText(percent: number): string {
  return percent === 0
    ? 'Sem fundo de reserva no recibo'
    : `${formatDecimalInput(percent)}% das despesas dos grupos marcados como base`;
}

/** O mês de início da tarifa que vale em `month`: a mais recente que já tinha começado. Nulo antes da primeira. */
export function inForceMonth(tariffs: readonly ConsumptionTariff[], month: string): string | null {
  const started = tariffs.filter((tariff) => tariff.validFrom <= month);
  return started.length === 0
    ? null
    : started.reduce((a, b) => (a.validFrom >= b.validFrom ? a : b)).validFrom;
}
