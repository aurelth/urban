import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RegistrationRequest } from '../../../core/admin/registration-requests.models';
import { describeProblem } from '../../../core/http/problem-message';
import { ApiProblem } from '../../../core/http/api-problem';
import { DECISION_MESSAGES } from './registration-requests.messages';
import { RegistrationRequestsStore } from './registration-requests.store';
import { formatRequestedAt } from './requested-at';

/** Pedidos de cadastro de síndico que aguardam o administrador. A recusa é final, por isso pede confirmação; aprovar não. */
@Component({
  selector: 'urban-registration-requests-page',
  imports: [MatButton, MatIcon],
  templateUrl: './registration-requests.page.html',
  providers: [RegistrationRequestsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegistrationRequestsPage implements OnInit {
  protected readonly store = inject(RegistrationRequestsStore);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  /** Pedido cuja recusa espera confirmação. Só um por vez. */
  protected readonly confirmingId = signal<string | null>(null);

  protected readonly formatDate = formatRequestedAt;

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected async approve(request: RegistrationRequest): Promise<void> {
    await this.decide(
      () => this.store.approve(request),
      `Pedido de ${request.name} aprovado. Enviamos o link de confirmação para ${request.email}`,
    );
  }

  protected askToReject(request: RegistrationRequest): void {
    this.clearMessages();
    this.confirmingId.set(request.id);
  }

  protected cancelReject(): void {
    this.confirmingId.set(null);
  }

  protected async reject(request: RegistrationRequest): Promise<void> {
    await this.decide(
      () => this.store.reject(request),
      `Pedido de ${request.name} recusado. Nada foi enviado à pessoa`,
    );
  }

  private async decide(run: () => Promise<ApiProblem | null>, success: string): Promise<void> {
    this.clearMessages();
    this.confirmingId.set(null);
    const problem = await run();
    if (problem === null) {
      this.notice.set(success);
    } else {
      this.actionError.set(describeProblem(problem, DECISION_MESSAGES));
    }
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
