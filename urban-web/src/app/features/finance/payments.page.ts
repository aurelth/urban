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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { formatMoney } from '../../core/finance/money';
import { addMonths, formatDate, isMonthInWindow, monthTitle } from '../../core/finance/month';
import { ChargeSummary, PaymentsStatusFilter } from '../../core/finance/payments.models';
import { describeProblem } from '../../core/http/problem-message';
import { MenuSelect } from '../../shared/ui/menu-select/menu-select';
import { unitLabel } from '../units/unit-values';
import { openChargePaymentsDialog } from './charge-payments.dialog';
import { openRegisterPaymentDialog } from './register-payment.dialog';
import { PaymentsStore } from './payments.store';
import { REGISTER_PAYMENT_MESSAGES } from './payments.messages';
import { STATUS_FILTERS, chargeStatusView } from './payments-values';
import { initialMonth } from './readings-values';

const FILTER_VALUES: readonly string[] = STATUS_FILTERS.map((option) => option.value);

/**
 * Pagamentos de um mês fechado: o resumo (cobrado, recebido, a receber), uma linha por unidade e, em cada uma, registrar um
 * pagamento (vários por cobrança, até o saldo) e ver o histórico, onde se estorna com motivo. Mês que ainda não foi
 * fechado não tem cobranças: a tela explica e leva aos lançamentos do mês.
 */
@Component({
  selector: 'urban-payments-page',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon, MatFormField, MatLabel, MenuSelect],
  templateUrl: './payments.page.html',
  providers: [PaymentsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentsPage implements OnInit {
  protected readonly store = inject(PaymentsStore);
  private readonly injector = inject(Injector);
  private readonly now = inject(NOW);
  private readonly route = inject(ActivatedRoute);

  protected readonly month = signal(
    initialMonth(this.route.snapshot.queryParamMap.get('mes'), this.now()),
  );
  protected readonly notice = signal<string | null>(null);

  protected readonly title = monthTitle;
  protected readonly money = formatMoney;
  protected readonly date = formatDate;
  protected readonly unitLabel = unitLabel;
  protected readonly statusView = chargeStatusView;
  protected readonly statusFilters = STATUS_FILTERS;

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

  protected onStatus(value: string): void {
    if (FILTER_VALUES.includes(value)) {
      this.notice.set(null);
      void this.store.filter(value as PaymentsStatusFilter);
    }
  }

  protected clearFilter(): void {
    this.notice.set(null);
    void this.store.filter('all');
  }

  protected goTo(page: number): void {
    this.notice.set(null);
    void this.store.goToPage(page);
  }

  protected async register(charge: ChargeSummary): Promise<void> {
    const label = unitLabel(charge);
    let registered = 0;
    const saved = await openRegisterPaymentDialog(this.injector, {
      label,
      month: this.month(),
      balance: charge.balance,
      save: async (payload) => {
        const problem = await this.store.register(charge.chargeId, payload);
        registered = payload.amount;
        return problem === null ? null : describeProblem(problem, REGISTER_PAYMENT_MESSAGES);
      },
    });
    if (saved) {
      this.notice.set(`Pagamento de ${formatMoney(registered)} registrado em ${label}`);
    }
  }

  protected async showPayments(charge: ChargeSummary): Promise<void> {
    const changed = await openChargePaymentsDialog(this.injector, {
      chargeId: charge.chargeId,
      label: unitLabel(charge),
    });
    if (changed) {
      await this.store.refresh();
      this.notice.set('Estorno registrado');
    }
  }

  private async show(month: string): Promise<void> {
    this.notice.set(null);
    this.month.set(month);
    await this.store.show(month);
  }
}
