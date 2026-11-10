import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import {
  ApportionmentBasis,
  ExpenseEntry,
  SettableEntryStatus,
} from '../../core/finance/finance.models';
import { formatMoney } from '../../core/finance/money';
import {
  addMonths,
  currentMonth,
  formatDate,
  isMonthInWindow,
  monthTitle,
} from '../../core/finance/month';
import { describeProblem } from '../../core/http/problem-message';
import { unitLabel } from '../units/unit-values';
import { basisLabel, chargeShare, chargesTotal, lineDetail } from './closing';
import { openCloseMonthDialog } from './close-month.dialog';
import { openEntryAmountDialog } from './entry-amount.dialog';
import {
  STATUS_CHOICES,
  isChoiceDisabled,
  statusIcon,
  statusLabel,
  statusMenuTone,
  statusTone,
} from './entry-status';
import { FINANCE_ACTION_MESSAGES, FINANCE_FORM_MESSAGES } from './finance.messages';
import { MonthStore } from './month.store';
import { openReopenMonthDialog } from './reopen-month.dialog';

/**
 * Lançamentos de um mês: navegação entre meses (5 anos para cada lado), total por grupo, vencimento e situação de
 * pagamento de cada lançamento. Os lançamentos do mês são gerados sozinhos ao entrar na tela ou trocar de mês, sem
 * botão. As despesas variáveis sem valor mostram a sugestão do mês anterior. Com todos os valores informados, o mês
 * pode ser fechado: o total é dividido entre as unidades e os valores ficam travados até o mês ser reaberto.
 */
@Component({
  selector: 'urban-month-page',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger],
  templateUrl: './month.page.html',
  providers: [MonthStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonthPage implements OnInit {
  protected readonly store = inject(MonthStore);
  private readonly injector = inject(Injector);
  private readonly now = inject(NOW);

  protected readonly month = signal(currentMonth(this.now()));
  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly title = monthTitle;
  protected readonly money = formatMoney;
  protected readonly date = formatDate;
  protected readonly choices = STATUS_CHOICES;
  protected readonly statusLabel = statusLabel;
  protected readonly statusIcon = statusIcon;
  protected readonly statusTone = statusTone;
  protected readonly statusMenuTone = statusMenuTone;
  protected readonly isChoiceDisabled = isChoiceDisabled;
  protected readonly unitLabel = unitLabel;
  protected readonly chargeShare = chargeShare;
  protected readonly chargesTotal = chargesTotal;
  protected readonly lineDetail = lineDetail;
  protected readonly basisName = (basis: ApportionmentBasis): string =>
    basisLabel(basis).toLowerCase();

  ngOnInit(): void {
    void this.show(this.month());
  }

  protected canGo(delta: number): boolean {
    return isMonthInWindow(addMonths(this.month(), delta), this.now());
  }

  protected go(delta: number): Promise<void> {
    return this.show(addMonths(this.month(), delta));
  }

  protected reload(): Promise<void> {
    return this.show(this.month());
  }

  protected async setStatus(target: ExpenseEntry, status: SettableEntryStatus): Promise<void> {
    this.clearMessages();
    const problem = await this.store.setStatus(target, status);
    // Sem aviso de sucesso: a própria linha já mostra a nova situação.
    if (problem !== null) {
      this.actionError.set(describeProblem(problem, FINANCE_ACTION_MESSAGES));
    }
  }

  protected async setAmount(target: ExpenseEntry): Promise<void> {
    this.clearMessages();
    await openEntryAmountDialog(this.injector, {
      entry: target,
      save: async (amount) => {
        const problem = await this.store.setAmount(target, amount);
        if (problem !== null) {
          return describeProblem(problem, FINANCE_FORM_MESSAGES);
        }
        this.notice.set(target.needsAmount ? 'Valor informado' : 'Valor corrigido');
        return null;
      },
    });
  }

  protected async closeMonth(): Promise<void> {
    this.clearMessages();
    const data = this.store.data();
    if (data === null) {
      return;
    }
    await openCloseMonthDialog(this.injector, {
      month: this.month(),
      total: data.total,
      save: async (dueDate) => {
        const problem = await this.store.close(dueDate);
        if (problem !== null) {
          return {
            message: describeProblem(problem, FINANCE_FORM_MESSAGES),
            readingsPending: problem.code === 'closing.readings_pending',
          };
        }
        this.notice.set('Mês fechado');
        return null;
      },
    });
  }

  protected async reopenMonth(): Promise<void> {
    this.clearMessages();
    await openReopenMonthDialog(this.injector, {
      month: this.month(),
      save: async () => {
        const problem = await this.store.reopen();
        if (problem !== null) {
          return describeProblem(problem, FINANCE_FORM_MESSAGES);
        }
        this.notice.set('Mês reaberto');
        return null;
      },
    });
  }

  private async show(month: string): Promise<void> {
    this.clearMessages();
    this.month.set(month);
    await this.store.show(month);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
