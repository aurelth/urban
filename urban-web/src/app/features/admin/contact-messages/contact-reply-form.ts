import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { map, startWith } from 'rxjs';
import { ContactMessage } from '../../../core/admin/contact-messages.models';
import { describeProblem } from '../../../core/http/problem-message';
import { focusFirstInvalidInput } from '../../../shared/forms/focus-first-invalid';
import { requiredText } from '../../../shared/forms/required-text.validator';
import { trimmedLength } from '../../../shared/forms/text-length.validator';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';
import { REPLY_BODY_ERRORS, REPLY_MESSAGES } from './contact-messages.messages';
import { ContactMessagesStore } from './contact-messages.store';
import {
  ATTACHMENT_ACCEPT,
  ATTACHMENT_HINT,
  ATTACHMENT_LIMITS,
  REPLY_BODY_LIMITS,
  checkAttachments,
  formatFileSize,
} from './reply-attachments';

/**
 * Resposta do administrador a uma mensagem: texto e até 3 anexos, enviados por e-mail ao síndico. Os anexos são conferidos
 * aqui só para avisar na hora; quem decide é a API, que olha o conteúdo do arquivo. Nada é enviado nem registrado quando a API
 * recusa, então o texto e os anexos ficam como estavam para a pessoa tentar de novo.
 */
@Component({
  selector: 'urban-contact-reply-form',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatIconButton,
    MatFormField,
    MatLabel,
    MatHint,
    MatError,
    MatInput,
    MatIcon,
    FormSubmit,
  ],
  templateUrl: './contact-reply-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactReplyForm {
  readonly message = input.required<ContactMessage>();
  /** A resposta foi enviada e a lista já foi atualizada. */
  readonly sent = output<void>();
  /** A pessoa desistiu; o texto e os anexos se perdem. */
  readonly cancelled = output<void>();

  private readonly store = inject(ContactMessagesStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    body: ['', [requiredText, trimmedLength(REPLY_BODY_LIMITS.min, REPLY_BODY_LIMITS.max)]],
  });

  protected readonly bodyLimits = REPLY_BODY_LIMITS;
  protected readonly bodyErrors = REPLY_BODY_ERRORS;
  protected readonly bodyLength = toSignal(
    this.form.controls.body.valueChanges.pipe(
      startWith(''),
      map((value) => value.trim().length),
    ),
    { requireSync: true },
  );

  protected readonly accept = ATTACHMENT_ACCEPT;
  protected readonly hint = ATTACHMENT_HINT;
  protected readonly maxFiles = ATTACHMENT_LIMITS.maxFiles;
  protected readonly formatSize = formatFileSize;

  protected readonly files = signal<readonly File[]>([]);
  protected readonly attachmentProblems = signal<readonly string[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected onPicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const result = checkAttachments(this.files(), Array.from(input.files ?? []));
    this.files.set(result.files);
    this.attachmentProblems.set(result.problems);
    // Sem limpar, escolher o mesmo arquivo de novo (depois de removê-lo) não dispara o evento.
    input.value = '';
  }

  protected remove(index: number): void {
    this.files.update((files) => files.filter((_, position) => position !== index));
    this.attachmentProblems.set([]);
  }

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
    const problem = await this.store.reply(
      this.message(),
      this.form.controls.body.value,
      this.files(),
    );
    this.submitting.set(false);
    if (problem === null) {
      this.sent.emit();
    } else {
      this.error.set(describeProblem(problem, REPLY_MESSAGES));
    }
  }
}
