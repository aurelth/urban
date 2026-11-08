import { ApportionmentBasis } from '../../core/finance/finance.models';
import { formatArea, formatPercent } from '../units/unit-values';

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
