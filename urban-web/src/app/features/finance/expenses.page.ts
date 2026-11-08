import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { Expense, ExpenseGroup, ExpenseStatusFilter } from '../../core/finance/finance.models';
import { currentMonth } from '../../core/finance/month';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { openEndExpenseDialog } from './end-expense.dialog';
import { openExpenseAmountDialog } from './expense-amount.dialog';
import {
  amountText,
  canAdjust,
  canEnd,
  canSetInstallment,
  dueText,
  kindLabel,
  periodText,
} from './expense-values';
import { openExpenseDialog } from './expense.dialog';
import { ExpensesStore } from './expenses.store';
import { FINANCE_FORM_MESSAGES } from './finance.messages';
import { openGroupDialog } from './group.dialog';
import { openInstallmentDialog } from './installment.dialog';

const STATUSES: readonly ExpenseStatusFilter[] = ['active', 'ended', 'all'];

/**
 * Despesas do condomínio: grupos, filtro por situação e a lista com as ações de cadastrar, editar, reajustar,
 * alterar parcela e encerrar. Toda gravação acontece num diálogo, que fica aberto com o erro até dar certo.
 */
@Component({
  selector: 'urban-expenses-page',
  imports: [RouterLink, MatButton, MatIconButton, MatFormField, MatLabel, MatIcon, MatInput],
  templateUrl: './expenses.page.html',
  providers: [ExpensesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpensesPage implements OnInit {
  protected readonly store = inject(ExpensesStore);
  private readonly injector = inject(Injector);
  private readonly now = inject(NOW);

  protected readonly notice = signal<string | null>(null);

  protected readonly kind = kindLabel;
  protected readonly amount = amountText;
  protected readonly period = periodText;
  protected readonly due = dueText;
  protected readonly canAdjust = canAdjust;
  protected readonly canSetInstallment = canSetInstallment;
  protected readonly canEnd = canEnd;

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected onStatus(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    void this.store.filterBy(STATUSES.find((candidate) => candidate === value) ?? 'active');
  }

  protected async createGroup(): Promise<void> {
    this.notice.set(null);
    await openGroupDialog(this.injector, {
      group: null,
      save: (name) => this.saved(this.store.createGroup(name), 'Grupo criado'),
    });
  }

  protected async renameGroup(target: ExpenseGroup): Promise<void> {
    this.notice.set(null);
    await openGroupDialog(this.injector, {
      group: target,
      save: (name) => this.saved(this.store.renameGroup(target, name), 'Grupo renomeado'),
    });
  }

  protected async create(): Promise<void> {
    this.notice.set(null);
    await openExpenseDialog(this.injector, {
      expense: null,
      groups: this.store.groups(),
      month: this.month(),
      save: (payload) => this.saved(this.store.createExpense(payload), 'Despesa cadastrada'),
    });
  }

  protected async edit(target: Expense): Promise<void> {
    this.notice.set(null);
    await openExpenseDialog(this.injector, {
      expense: target,
      groups: this.store.groups(),
      month: this.month(),
      save: ({ description, supplier, groupId, dueDay }) =>
        this.saved(
          this.store.updateExpense(target, { description, supplier, groupId, dueDay }),
          'Despesa atualizada',
        ),
    });
  }

  protected async adjust(target: Expense): Promise<void> {
    this.notice.set(null);
    await openExpenseAmountDialog(this.injector, {
      expense: target,
      month: this.month(),
      save: (fromMonth, amount) =>
        this.saved(this.store.changeAmount(target, fromMonth, amount), 'Valor reajustado'),
    });
  }

  protected async setInstallment(target: Expense): Promise<void> {
    this.notice.set(null);
    await openInstallmentDialog(this.injector, {
      expense: target,
      month: this.month(),
      save: (number, amount) =>
        this.saved(this.store.setInstallment(target, number, amount), 'Parcela atualizada'),
    });
  }

  protected async end(target: Expense): Promise<void> {
    this.notice.set(null);
    await openEndExpenseDialog(this.injector, {
      expense: target,
      month: this.month(),
      save: (lastMonth) => this.saved(this.store.end(target, lastMonth), 'Despesa encerrada'),
    });
  }

  private month(): string {
    return currentMonth(this.now());
  }

  /** Texto do erro para o diálogo, ou nulo quando a gravação deu certo (e então o aviso aparece na tela). */
  private async saved(
    request: Promise<ApiProblem | null>,
    success: string,
  ): Promise<string | null> {
    const problem = await request;
    if (problem === null) {
      this.notice.set(success);
      return null;
    }
    return describeProblem(problem, FINANCE_FORM_MESSAGES);
  }
}
