import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatRadioButton, MatRadioGroup } from '@angular/material/radio';
import { map, startWith } from 'rxjs';
import {
  ANNOUNCEMENT_BODY_MAX,
  ANNOUNCEMENT_TITLE_MAX,
  Announcement,
  AnnouncementCategory,
  AnnouncementPayload,
  AnnouncementScope,
} from '../../core/announcements/announcements.models';
import { Unit } from '../../core/units/units.models';
import { requiredText } from '../../shared/forms/required-text.validator';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { SelectField, SelectOption } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { unitLabel } from '../units/unit-values';
import { CATEGORY_OPTIONS } from './announcement-values';

/** O que o diálogo mostra e o que ele faz com o comunicado digitado. */
export interface AnnouncementDialogConfig {
  /** O comunicado que está sendo editado; nulo quando é um novo. */
  readonly announcement: Announcement | null;
  /** As unidades ativas, de onde saem os blocos e as unidades que o síndico pode escolher. */
  readonly units: readonly Unit[];
  /**
   * Grava o comunicado. Devolve nulo quando deu certo (o diálogo fecha) ou o texto do erro (o diálogo continua aberto,
   * com o que foi digitado, para a pessoa corrigir).
   */
  readonly save: (payload: AnnouncementPayload) => Promise<string | null>;
}

interface AnnouncementFormValue {
  title: string;
  category: AnnouncementCategory;
  scope: AnnouncementScope;
  block: string;
  unitId: string;
  body: string;
}

/** O bloco só é exigido no alcance de bloco, e a unidade só no de unidade. */
function requiredWhenScope(scope: AnnouncementScope) {
  return (control: AbstractControl<string>): ValidationErrors | null =>
    control.parent?.get('scope')?.value === scope && control.value === ''
      ? { required: true }
      : null;
}

/**
 * Criar e editar um comunicado. O alcance é escolhido em três passos: condomínio inteiro, um bloco ou uma unidade; só
 * o campo do alcance escolhido aparece. Na edição, o botão só habilita depois de alguma mudança.
 */
@Component({
  selector: 'urban-announcement-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatError,
    MatFormField,
    MatHint,
    MatIcon,
    MatInput,
    MatLabel,
    MatRadioButton,
    MatRadioGroup,
    SelectField,
    TextField,
  ],
  templateUrl: './announcement.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementDialog {
  protected readonly config = inject<AnnouncementDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<AnnouncementDialog, boolean>>(MatDialogRef);
  protected readonly editing = this.config.announcement !== null;

  private readonly initial = toFormValue(this.config.announcement);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: [this.initial.title, [requiredText, trimmedLength(1, ANNOUNCEMENT_TITLE_MAX)]],
    category: [this.initial.category],
    scope: [this.initial.scope],
    block: [this.initial.block, requiredWhenScope('block')],
    unitId: [this.initial.unitId, requiredWhenScope('unit')],
    body: [this.initial.body, [requiredText, trimmedLength(1, ANNOUNCEMENT_BODY_MAX)]],
  });

  protected readonly categoryOptions: readonly SelectOption[] = CATEGORY_OPTIONS;
  protected readonly blockOptions: readonly SelectOption[] = [
    ...new Set(this.config.units.flatMap((unit) => (unit.block === null ? [] : [unit.block]))),
  ]
    .sort((first, second) => first.localeCompare(second, 'pt-BR', { numeric: true }))
    .map((block) => ({ value: block, label: `Bloco ${block}` }));
  protected readonly unitOptions: readonly SelectOption[] = this.config.units.map((unit) => ({
    value: unit.id,
    label: unitLabel(unit),
  }));
  protected readonly hasBlocks = this.blockOptions.length > 0;
  protected readonly hasUnits = this.unitOptions.length > 0;

  protected readonly scope = toSignal(
    this.form.controls.scope.valueChanges.pipe(startWith(this.initial.scope)),
    { requireSync: true },
  );
  protected readonly bodyLength = toSignal(
    this.form.controls.body.valueChanges.pipe(
      startWith(this.initial.body),
      map((value) => value.trim().length),
    ),
    { requireSync: true },
  );
  protected readonly bodyMax = ANNOUNCEMENT_BODY_MAX;

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);
  protected readonly showBlock = computed(() => this.scope() === 'block');
  protected readonly showUnit = computed(() => this.scope() === 'unit');

  protected readonly titleErrors = {
    required: 'Informe o título',
    tooLong: `O título pode ter até ${ANNOUNCEMENT_TITLE_MAX} caracteres`,
  };
  protected readonly blockErrors = { required: 'Escolha o bloco' };
  protected readonly unitErrors = { required: 'Escolha a unidade' };

  constructor() {
    const { scope, block, unitId } = this.form.controls;
    scope.valueChanges.subscribe(() => {
      block.updateValueAndValidity();
      unitId.updateValueAndValidity();
    });
    this.form.valueChanges.subscribe(() => {
      this.changed.set(!sameValue(this.initial, this.form.getRawValue()));
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
    const message = await this.config.save(toPayload(this.form.getRawValue()));
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

/**
 * Abre o diálogo como modal que não fecha ao clicar fora nem com Esc, para ninguém perder o que digitou sem querer.
 * Responde verdadeiro quando o comunicado foi gravado e falso quando a pessoa cancelou.
 */
export function openAnnouncementDialog(
  injector: Injector,
  config: AnnouncementDialogConfig,
): Promise<boolean> {
  const ref = injector
    .get(MatDialog)
    .open<AnnouncementDialog, AnnouncementDialogConfig, boolean>(AnnouncementDialog, {
      data: config,
      disableClose: true,
      role: 'dialog',
      width: '640px',
      maxWidth: 'calc(100vw - 32px)',
    });
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((saved) => resolve(saved === true));
  });
}

function toFormValue(item: Announcement | null): AnnouncementFormValue {
  return {
    title: item?.title ?? '',
    category: item?.category ?? 'general',
    scope: item?.scope ?? 'condominium',
    block: item?.block ?? '',
    unitId: item?.unitId ?? '',
    body: item?.body ?? '',
  };
}

/** Só o que vale para o alcance escolhido conta: bloco e unidade que sobraram de outra escolha não são mudança. */
function significant(value: AnnouncementFormValue): AnnouncementFormValue {
  return {
    title: value.title.trim(),
    category: value.category,
    scope: value.scope,
    block: value.scope === 'block' ? value.block : '',
    unitId: value.scope === 'unit' ? value.unitId : '',
    body: value.body.trim(),
  };
}

function sameValue(first: AnnouncementFormValue, second: AnnouncementFormValue): boolean {
  return JSON.stringify(significant(first)) === JSON.stringify(significant(second));
}

/** Só chega aqui com o formulário válido. */
function toPayload(value: AnnouncementFormValue): AnnouncementPayload {
  const { title, category, scope, block, unitId, body } = significant(value);
  return {
    title,
    body,
    category,
    scope,
    block: scope === 'block' ? block : null,
    unitId: scope === 'unit' ? unitId : null,
  };
}
