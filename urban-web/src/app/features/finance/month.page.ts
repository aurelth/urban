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
import { RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { ExpenseEntry } from '../../core/finance/finance.models';
import { formatMoney } from '../../core/finance/money';
import { addMonths, canOpenMonth, currentMonth, monthTitle } from '../../core/finance/month';
import { describeProblem } from '../../core/http/problem-message';
import { openEntryAmountDialog } from './entry-amount.dialog';
import { FINANCE_ACTION_MESSAGES, FINANCE_FORM_MESSAGES } from './finance.messages';
import { MonthStore } from './month.store';

/**
 * Lançamentos de um mês: navegação entre meses, abrir o mês (gerar o que falta, sem mexer no que já existe), total
 * por grupo e a conferência dos valores. Os pendentes (despesas variáveis sem valor) mostram a sugestão do mês anterior.
 */
@Component({
  selector: 'urban-month-page',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon],
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

  ngOnInit(): void {
    void this.show(this.month());
  }

  protected canGoForward(): boolean {
    return canOpenMonth(addMonths(this.month(), 1), this.now());
  }

  protected go(delta: number): Promise<void> {
    return this.show(addMonths(this.month(), delta));
  }

  protected reload(): Promise<void> {
    return this.show(this.month());
  }

  protected isOpened(): boolean {
    return (this.store.data()?.entries.length ?? 0) > 0;
  }

  protected async openMonth(): Promise<void> {
    const reopening = this.isOpened();
    this.clearMessages();
    const problem = await this.store.open();
    if (problem === null) {
      this.notice.set(reopening ? 'Lançamentos atualizados' : 'Mês aberto');
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
        this.notice.set(target.isPending ? 'Valor informado' : 'Valor corrigido');
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
