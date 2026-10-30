import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { MatAnchor, MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountApi } from '../../../core/auth/account.api';
import { SessionStore } from '../../../core/auth/session.store';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { LinkProblemNotice } from '../link-problem/link-problem.notice';

const CONFIRM_MESSAGES: MessagesByCode = {
  'identity.email_change.invalid': 'Link inválido ou já usado. Peça a troca de e-mail de novo',
  'identity.email_change.expired': 'Link expirado. Peça a troca de e-mail de novo',
};

const MISSING_TOKEN_MESSAGE =
  'Este link está incompleto. Abra o link do e-mail de novo ou peça a troca de e-mail de novo';

type Step = 'confirming' | 'done' | 'link-problem' | 'failed';

/**
 * Destino do link que chega ao e-mail novo. Abrir o link já confirma (a rota é pública: ler a caixa do endereço novo é a
 * prova). Como a API encerra todas as sessões da conta ao trocar o e-mail, este navegador também sai, sem chamar a API.
 * Um link recusado não mexe na sessão de quem está logado.
 */
@Component({
  selector: 'urban-confirm-email-change-page',
  imports: [MatAnchor, MatButton, MatCard, MatCardContent, MatIcon, RouterLink, LinkProblemNotice],
  templateUrl: './confirm-email-change.page.html',
})
export class ConfirmEmailChangePage implements OnInit {
  /** Token do link do e-mail (?token=...). */
  readonly token = input<string>();

  private readonly api = inject(AccountApi);
  private readonly session = inject(SessionStore);

  protected readonly step = signal<Step>('confirming');
  protected readonly message = signal<string | null>(null);
  protected readonly missingToken = computed(() => !this.token());
  protected readonly missingMessage = MISSING_TOKEN_MESSAGE;

  ngOnInit(): void {
    if (this.token()) {
      void this.confirm();
    }
  }

  protected async confirm(): Promise<void> {
    const token = this.token();
    if (!token) {
      return;
    }
    this.step.set('confirming');
    this.message.set(null);
    try {
      await firstValueFrom(this.api.confirmEmailChange(token));
      this.session.endSession();
      this.step.set('done');
    } catch (error) {
      const problem = toApiProblem(error);
      this.message.set(describeProblem(problem, CONFIRM_MESSAGES));
      const linkProblem = problem.code !== null && Object.hasOwn(CONFIRM_MESSAGES, problem.code);
      this.step.set(linkProblem ? 'link-problem' : 'failed');
    }
  }
}
