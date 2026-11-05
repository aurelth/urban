import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatRadioButton, MatRadioChange, MatRadioGroup } from '@angular/material/radio';
import { ApportionmentMethod, UnitsSummary } from '../../core/units/units.models';
import { formatPercent } from './unit-values';

interface MethodOption {
  readonly method: ApportionmentMethod;
  readonly label: string;
  readonly description: string;
}

const OPTIONS: readonly MethodOption[] = [
  {
    method: 'idealFraction',
    label: 'Fração ideal',
    description: 'Cada unidade paga conforme a sua fração ideal',
  },
  {
    method: 'equalShare',
    label: 'Valor igual',
    description: 'Todas as unidades ativas pagam o mesmo valor',
  },
  { method: 'area', label: 'Área', description: 'Cada unidade paga conforme a sua área' },
];

/**
 * Escolha do método de rateio e, ao lado de cada opção, se as unidades já têm o que ele precisa. A escolha vale
 * mesmo com o método ainda pendente: o aviso só diz o que falta preencher. Quem usa o cartão grava a troca.
 */
@Component({
  selector: 'urban-apportionment-card',
  imports: [MatCard, MatCardContent, MatIcon, MatRadioGroup, MatRadioButton],
  templateUrl: './apportionment.card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApportionmentCard {
  readonly summary = input.required<UnitsSummary>();
  readonly busy = input(false);
  readonly methodChange = output<ApportionmentMethod>();

  protected readonly options = OPTIONS;

  protected isReady(method: ApportionmentMethod): boolean {
    return this.summary().readiness.find((item) => item.method === method)?.ready ?? false;
  }

  protected readinessText(method: ApportionmentMethod): string {
    const summary = this.summary();
    if (this.isReady(method)) {
      return 'Pronto para usar';
    }
    if (summary.active === 0) {
      return 'Cadastre unidades ativas';
    }
    switch (method) {
      case 'idealFraction':
        return summary.withoutIdealFraction > 0
          ? count(summary.withoutIdealFraction, 'sem fração ideal')
          : `A soma das frações é ${formatPercent(summary.idealFractionTotal)}: ela precisa chegar a 100%`;
      case 'area':
        return count(summary.withoutArea, 'sem área');
      case 'equalShare':
        return 'Cadastre unidades ativas';
    }
  }

  protected choose(change: MatRadioChange): void {
    const method = change.value as ApportionmentMethod;
    if (method !== this.summary().apportionmentMethod) {
      this.methodChange.emit(method);
    }
  }
}

function count(total: number, what: string): string {
  return `${total} ${total === 1 ? 'unidade' : 'unidades'} ${what}`;
}
