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
import { ExpenseEntry, SettableEntryStatus } from '../../core/finance/finance.models';
import { formatMoney } from '../../core/finance/money';
import {
  addMonths,
  currentMonth,
  formatDate,
  isMonthInWindow,
  monthTitle,
} from '../../core/finance/month';
import { describeProblem } from '../../core/http/problem-message';
import { openEntryAmountDialog } from './entry-amount.dialog';
import {
  STATUS_CHOICES,
  isChoiceDisabled,
  statusIcon,
  statusLabel,
  statusNotice,
  statusTone,
} from './entry-status';
import { FINANCE_ACTION_MESSAGES, FINANCE_FORM_MESSAGES } from './finance.messages';
import { MonthStore } from './month.store';

/**
 * Lançamentos de um mês: navegação entre meses (5 anos para cada lado), total por grupo, vencimento e situação de
 * pagamento de cada lançamento. Os lançamentos do mês são gerados sozinhos ao entrar na tela ou trocar de mês, sem
 * botão. As despesas variáveis sem valor mostram a sugestão do mês anterior.
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
  protected readonly isChoiceDisabled = isChoiceDisabled;

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
    if (problem === null) {
      this.notice.set(statusNotice(status));
    } else {
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
