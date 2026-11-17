import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { ExpenseEntry } from '../../core/finance/finance.models';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';
import { NOTE_INVALID_MESSAGE } from './finance.messages';

/** O mesmo limite da API (`ExpenseEntry.NoteMaxLength`). */
export const NOTE_MAX_LENGTH = 200;

export interface EntryNoteDialogConfig {
  readonly entry: ExpenseEntry;
  /** Grava a observação (nulo a remove). Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (note: string | null) => Promise<string | null>;
}

/**
 * Escreve, corrige ou remove a observação de um lançamento: uma linha de texto que vale só para aquele mês e vai à
 * fatura, sob a despesa, junto com o fornecedor. O botão só habilita depois de mudar o texto; apagar tudo remove.
 */
@Component({
  selector: 'urban-entry-note-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    TextField,
  ],
  templateUrl: './entry-note.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryNoteDialog {
  protected readonly config = inject<EntryNoteDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<EntryNoteDialog, boolean>>(MatDialogRef);

  protected readonly explanation =
    'A observação aparece na fatura, sob a despesa, junto com o fornecedor. Deixe em branco para remover.';

  private readonly initial = (this.config.entry.note ?? '').trim();

  protected readonly form = inject(NonNullableFormBuilder).group({
    note: [this.initial, trimmedLength(0, NOTE_MAX_LENGTH)],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);
  private readonly length = signal(this.initial.length);
  protected readonly counter = computed(() => `${this.length()} de ${NOTE_MAX_LENGTH} caracteres`);

  protected readonly noteErrors = { tooLong: NOTE_INVALID_MESSAGE };

  constructor() {
    this.form.valueChanges.subscribe(() => {
      const text = this.typed();
      this.length.set(text.length);
      this.changed.set(text !== this.initial);
    });
  }

  protected async confirm(): Promise<void> {
    if (this.busy() || !this.changed()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const text = this.typed();
    const message = await this.config.save(text === '' ? null : text);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }

  private typed(): string {
    return this.form.getRawValue().note.trim();
  }
}

export function openEntryNoteDialog(
  injector: Injector,
  config: EntryNoteDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, EntryNoteDialog, config);
}
