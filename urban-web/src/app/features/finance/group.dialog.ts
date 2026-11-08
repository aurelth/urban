import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { ExpenseGroup, ExpenseGroupValues } from '../../core/finance/finance.models';
import { requiredText } from '../../shared/forms/required-text.validator';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';

const NAME_MAX_LENGTH = 60;

export interface GroupDialogConfig {
  /** O grupo que está sendo renomeado; nulo quando é um grupo novo. */
  readonly group: ExpenseGroup | null;
  /** Grava o nome e a marca. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (values: ExpenseGroupValues) => Promise<string | null>;
}

/** Cria ou edita um grupo de despesas. Na edição, o botão só habilita depois de mudar o nome ou a marca do fundo de reserva. */
@Component({
  selector: 'urban-group-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCheckbox,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    TextField,
  ],
  templateUrl: './group.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupDialog {
  protected readonly config = inject<GroupDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<GroupDialog, boolean>>(MatDialogRef);
  protected readonly editing = this.config.group !== null;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.config.group?.name ?? '', [requiredText, Validators.maxLength(NAME_MAX_LENGTH)]],
    reserveFundBase: [this.config.group?.reserveFundBase ?? false],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly nameErrors = {
    required: 'Informe o nome do grupo',
    maxlength: 'O nome pode ter até 60 caracteres',
  };

  constructor() {
    this.form.valueChanges.subscribe(() => {
      const { name, reserveFundBase } = this.form.getRawValue();
      this.changed.set(
        name.trim() !== (this.config.group?.name ?? '') ||
          reserveFundBase !== (this.config.group?.reserveFundBase ?? false),
      );
    });
  }

  protected async confirm(): Promise<void> {
    if (this.busy() || (this.editing && !this.changed())) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const { name, reserveFundBase } = this.form.getRawValue();
    const message = await this.config.save({ name: name.trim(), reserveFundBase });
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openGroupDialog(injector: Injector, config: GroupDialogConfig): Promise<boolean> {
  return openFinanceDialog(injector, GroupDialog, config);
}
