import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  ElementRef,
  TemplateRef,
  contentChild,
  inject,
  input,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { Observable, firstValueFrom } from 'rxjs';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { emailFormat } from '../../../shared/forms/email.validator';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { EmailField } from '../../../shared/ui/email-field/email.field';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';

const LINK_REQUEST_MESSAGES: MessagesByCode = {
  'identity.email.invalid': 'Informe um e-mail válido',
};

/** O que o modelo do aviso recebe: o e-mail para onde o link foi pedido. */
export interface SentNoticeContext {
  $implicit: string;
}

/**
 * Cartão de quem pede um link por e-mail (cadastro e recuperação de senha): formulário com o
 * e-mail e, depois do envio, o aviso para verificar a caixa de entrada. Cada tela informa o
 * título, a introdução e a chamada à API (`send`), e escreve o próprio aviso e o próprio rodapé:
 *
 * - `<ng-template #notice let-email>`: o aviso mostrado depois do envio (obrigatório);
 * - conteúdo solto: o rodapé do formulário;
 * - `sentAction`: ação extra do aviso, como um link de volta.
 */
@Component({
  selector: 'urban-link-request-card',
  imports: [
    ReactiveFormsModule,
    NgTemplateOutlet,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    EmailField,
    FormSubmit,
  ],
  templateUrl: './link-request.card.html',
})
export class LinkRequestCard {
  readonly heading = input.required<string>();
  readonly intro = input.required<string>();
  readonly send = input.required<(email: string) => Observable<void>>();

  protected readonly notice = contentChild.required<TemplateRef<SentNoticeContext>>('notice');

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** E-mail para onde o link foi pedido. Preenchido, a tela mostra o aviso no lugar do formulário. */
  protected readonly sentTo = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.form);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      const { email } = this.form.getRawValue();
      await firstValueFrom(this.send()(email));
      this.sentTo.set(email);
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), LINK_REQUEST_MESSAGES));
    } finally {
      this.submitting.set(false);
    }
  }

  protected changeEmail(): void {
    this.sentTo.set(null);
  }
}
