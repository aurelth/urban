import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { firstValueFrom, map, startWith } from 'rxjs';
import { ContactApi } from '../../core/contact/contact.api';
import { toApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { focusFirstInvalidInput } from '../../shared/forms/focus-first-invalid';
import { requiredText } from '../../shared/forms/required-text.validator';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { FormSubmit } from '../../shared/ui/form-submit/form-submit';
import { TextField } from '../../shared/ui/text-field/text.field';
import { BODY_LIMITS, SEND_MESSAGES, SUBJECT_ERRORS, SUBJECT_LIMITS } from './contact.messages';

/**
 * "Contato": o síndico escreve ao dono da plataforma (pedir a correção do CNPJ, tirar uma dúvida). É um registro, sem
 * conversa na tela: a resposta chega por e-mail ou telefone, e por isso a confirmação diz isso.
 */
@Component({
  selector: 'urban-contact-page',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatFormField,
    MatLabel,
    MatHint,
    MatError,
    MatInput,
    MatIcon,
    TextField,
    FormSubmit,
  ],
  templateUrl: './contact.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactPage {
  private readonly api = inject(ContactApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    subject: ['', [requiredText, trimmedLength(SUBJECT_LIMITS.min, SUBJECT_LIMITS.max)]],
    body: ['', [requiredText, trimmedLength(BODY_LIMITS.min, BODY_LIMITS.max)]],
  });

  protected readonly subjectErrors = SUBJECT_ERRORS;
  protected readonly bodyLimits = BODY_LIMITS;
  protected readonly bodyLength = toSignal(
    this.form.controls.body.valueChanges.pipe(
      startWith(''),
      map((value) => value.trim().length),
    ),
    { requireSync: true },
  );

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly sent = signal(false);

  protected async send(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // O foco procura a marca de campo inválido, que só chega ao HTML depois de a tela se atualizar.
      this.changeDetector.detectChanges();
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const { subject, body } = this.form.getRawValue();
      await firstValueFrom(this.api.send(subject, body));
      this.sent.set(true);
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), SEND_MESSAGES));
    } finally {
      this.submitting.set(false);
    }
  }

  protected writeAnother(): void {
    this.form.reset();
    this.sent.set(false);
  }
}
