import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import {
  InvoiceDelivery,
  InvoiceDeliveryState,
} from '../../core/finance/invoice-deliveries.models';
import { addMonths, isMonthInWindow, monthTitle } from '../../core/finance/month';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { formatRequestedAt } from '../admin/registration-requests/requested-at';
import { unitLabel } from '../units/unit-values';
import { INVOICE_DELIVERY_MESSAGES } from './invoice-deliveries.messages';
import { InvoiceDeliveriesStore } from './invoice-deliveries.store';
import {
  deliveryAction,
  deliveryFailureText,
  deliveryStateView,
  sendSummary,
} from './invoice-delivery-values';
import { initialMonth } from './readings-values';

/** De quanto em quanto tempo a tela lê a lista enquanto há fatura na fila. */
const POLL_INTERVAL_MS = 5_000;

interface CountCard {
  readonly testId: string;
  readonly state: InvoiceDeliveryState;
  readonly label: string;
}

const COUNT_CARDS: readonly CountCard[] = [
  { testId: 'count-sent', state: 'sent', label: 'Enviadas' },
  { testId: 'count-queued', state: 'queued', label: 'Na fila' },
  { testId: 'count-failed', state: 'failed', label: 'Falharam' },
  { testId: 'count-not-sent', state: 'notSent', label: 'Não enviadas' },
  { testId: 'count-no-recipient', state: 'noRecipient', label: 'Sem e-mail' },
];

/**
 * Envio das faturas de um mês fechado por e-mail: quantas unidades estão em cada situação, o envio de todas as que faltam
 * (com confirmação) e, em cada unidade, enviar, enviar de novo ou reenviar a fatura. Enquanto há fatura na fila, a tela lê a
 * lista sozinha até ela ser enviada. Mês que ainda não foi fechado não tem faturas: a tela explica e leva aos lançamentos.
 */
@Component({
  selector: 'urban-invoice-deliveries-page',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon],
  templateUrl: './invoice-deliveries.page.html',
  providers: [InvoiceDeliveriesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDeliveriesPage implements OnInit {
  protected readonly store = inject(InvoiceDeliveriesStore);
  private readonly now = inject(NOW);
  private readonly route = inject(ActivatedRoute);

  protected readonly month = signal(
    initialMonth(this.route.snapshot.queryParamMap.get('mes'), this.now()),
  );
  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  /** A pergunta "enviar as faturas do mês?" está na tela. */
  protected readonly confirming = signal(false);

  protected readonly title = monthTitle;
  protected readonly unitLabel = unitLabel;
  protected readonly stateView = deliveryStateView;
  protected readonly action = deliveryAction;
  protected readonly cards = COUNT_CARDS;

  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private closed = false;

  constructor() {
    effect(() => {
      if (this.store.waiting()) {
        this.schedulePoll();
      }
    });
    inject(DestroyRef).onDestroy(() => {
      this.closed = true;
      if (this.pollTimer !== null) {
        clearTimeout(this.pollTimer);
      }
    });
  }

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

  protected goTo(page: number): void {
    this.clearMessages();
    void this.store.goToPage(page);
  }

  protected invoices(count: number): string {
    return `${count} ${count === 1 ? 'fatura' : 'faturas'}`;
  }

  protected pendingText(): string {
    const pending = this.store.pending();
    return pending === 0 ? 'Nenhuma fatura a enviar' : `${this.invoices(pending)} a enviar`;
  }

  /** Quando o envio foi feito, ou por que falhou; as outras situações não têm o que acrescentar. */
  protected detail(item: InvoiceDelivery): string | null {
    if (item.state === 'sent' && item.sentAt !== null) {
      return formatRequestedAt(item.sentAt);
    }
    return item.state === 'failed' ? deliveryFailureText(item.failureReason) : null;
  }

  protected askToSend(): void {
    this.clearMessages();
    this.confirming.set(true);
  }

  protected cancelSend(): void {
    this.confirming.set(false);
  }

  protected async send(): Promise<void> {
    const { result, problem } = await this.store.sendMonth();
    this.confirming.set(false);
    if (problem !== null) {
      this.fail(problem);
    } else if (result !== null) {
      this.announce(sendSummary(result));
    }
  }

  protected async resend(item: InvoiceDelivery): Promise<void> {
    this.clearMessages();
    const problem = await this.store.resend(item.chargeId);
    if (problem === null) {
      this.announce(`Fatura de ${unitLabel(item)} entrou na fila de envio`);
    } else {
      this.fail(problem);
    }
  }

  /** Lê a lista daqui a pouco, se ainda houver fatura na fila, e repete até a fila esvaziar ou a tela fechar. */
  private schedulePoll(): void {
    if (this.pollTimer !== null || this.closed) {
      return;
    }
    this.pollTimer = setTimeout(() => void this.poll(), POLL_INTERVAL_MS);
  }

  private async poll(): Promise<void> {
    this.pollTimer = null;
    if (this.closed || !this.store.waiting()) {
      return;
    }
    await this.store.poll();
    if (this.store.waiting()) {
      this.schedulePoll();
    }
  }

  private announce(message: string): void {
    this.actionError.set(null);
    this.notice.set(message);
  }

  private fail(problem: ApiProblem): void {
    this.notice.set(null);
    this.actionError.set(describeProblem(problem, INVOICE_DELIVERY_MESSAGES));
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }

  private async show(month: string): Promise<void> {
    this.clearMessages();
    this.confirming.set(false);
    this.month.set(month);
    await this.store.show(month);
  }
}
