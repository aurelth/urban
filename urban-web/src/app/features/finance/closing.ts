import { ApportionmentBasis, ChargeLine } from '../../core/finance/finance.models';
import { formatArea, formatPercent } from '../units/unit-values';

const CUBIC_METERS = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 });
const CENTS = 100;

const LABELS: Readonly<Record<ApportionmentBasis, string>> = {
  idealFraction: 'Fração ideal',
  equalShare: 'Valor igual',
  area: 'Área',
};

/** O nome do método de rateio, o mesmo da tela de unidades. */
export function basisLabel(basis: ApportionmentBasis): string {
  return LABELS[basis];
}

/** Quanto a unidade pesou no rateio, no formato do método: "60%", "55,5 m²" ou "Parte igual". */
export function chargeShare(basis: ApportionmentBasis, weight: number): string {
  switch (basis) {
    case 'idealFraction':
      return formatPercent(weight);
    case 'area':
      return formatArea(weight);
    case 'equalShare':
      return 'Parte igual';
  }
}

/** Consumo ou leitura em m³, com até 3 casas (como o medidor): "17,649 m³". */
export function formatCubicMeters(value: number): string {
  return `${CUBIC_METERS.format(value)} m³`;
}

/** O consumo e as leituras de uma linha de água, esgoto ou gás; nulo nas linhas sem consumo. */
export function lineDetail(line: ChargeLine): string | null {
  if (line.quantity === null) {
    return null;
  }
  const consumption = formatCubicMeters(line.quantity);
  if (line.previousReading === null || line.currentReading === null) {
    return consumption;
  }
  const from = CUBIC_METERS.format(line.previousReading);
  const to = CUBIC_METERS.format(line.currentReading);
  return `${consumption} (leitura de ${from} a ${to})`;
}

/** A soma das cobranças em centavos, sem a sobra do ponto flutuante. */
export function chargesTotal(charges: readonly { readonly amount: number }[]): number {
  const cents = charges.reduce((sum, charge) => sum + Math.round(charge.amount * CENTS), 0);
  return cents / CENTS;
}
