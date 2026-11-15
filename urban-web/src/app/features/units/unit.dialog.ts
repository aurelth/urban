import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatRadioButton, MatRadioGroup } from '@angular/material/radio';
import { Unit, UnitKind, UnitPayload } from '../../core/units/units.models';
import { TextField } from '../../shared/ui/text-field/text.field';
import { requiredText } from '../../shared/forms/required-text.validator';
import { AREA_MESSAGE, FRACTION_MESSAGE } from './unit-batch.rows';
import { areaField, parkingSpotsField, percentField } from './unit-field.validators';
import {
  formatAreaInput,
  formatParkingSpotsInput,
  formatPercentInput,
  parseArea,
  parseParkingSpots,
  parsePercent,
  percentToFraction,
} from './unit-values';

const NUMBER_MAX_LENGTH = 20;
const BLOCK_MAX_LENGTH = 30;

/** O que o diálogo mostra e o que ele faz com a unidade digitada. */
export interface UnitDialogConfig {
  /** A unidade que está sendo editada; nula quando é um cadastro novo. */
  readonly unit: Unit | null;
  /**
   * Grava a unidade. Devolve nulo quando deu certo (o diálogo fecha) ou o texto do erro (o diálogo continua aberto,
   * com o que foi digitado, para a pessoa corrigir).
   */
  readonly save: (payload: UnitPayload) => Promise<string | null>;
}

interface UnitFormValue {
  block: string;
  number: string;
  type: UnitKind;
  idealFraction: string;
  area: string;
  parkingSpots: string;
}

/**
 * Cadastro e edição de uma unidade. A fração ideal é digitada em porcentagem e a área em m²; os dois são opcionais.
 * Na edição, o botão só habilita depois de alguma mudança.
 */
@Component({
  selector: 'urban-unit-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    MatRadioButton,
    MatRadioGroup,
    TextField,
  ],
  templateUrl: './unit.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnitDialog {
  protected readonly config = inject<UnitDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<UnitDialog, boolean>>(MatDialogRef);
  protected readonly editing = this.config.unit !== null;

  private readonly initial = toFormValue(this.config.unit);

  protected readonly form = inject(NonNullableFormBuilder).group({
    block: [this.initial.block, Validators.maxLength(BLOCK_MAX_LENGTH)],
    number: [this.initial.number, [requiredText, Validators.maxLength(NUMBER_MAX_LENGTH)]],
    type: [this.initial.type],
    idealFraction: [this.initial.idealFraction, percentField],
    area: [this.initial.area, areaField],
    parkingSpots: [this.initial.parkingSpots, parkingSpotsField],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly numberErrors = {
    required: 'Informe o número da unidade',
    maxlength: 'O número pode ter até 20 caracteres',
  };
  protected readonly blockErrors = { maxlength: 'O bloco pode ter até 30 caracteres' };
  protected readonly fractionErrors = { percent: FRACTION_MESSAGE };
  protected readonly areaErrors = { area: AREA_MESSAGE };
  protected readonly parkingSpotsErrors = {
    parkingSpotsTooMany: 'Informe até 5 vagas por unidade',
    parkingSpotsTooLong: 'Cada vaga pode ter até 20 caracteres',
    parkingSpotsDuplicate: 'A mesma vaga aparece mais de uma vez',
  };

  constructor() {
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
 * Responde verdadeiro quando a unidade foi gravada e falso quando a pessoa cancelou.
 */
export function openUnitDialog(injector: Injector, config: UnitDialogConfig): Promise<boolean> {
  const ref = injector.get(MatDialog).open<UnitDialog, UnitDialogConfig, boolean>(UnitDialog, {
    data: config,
    disableClose: true,
    role: 'dialog',
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
  });
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((saved) => resolve(saved === true));
  });
}

function toFormValue(unit: Unit | null): UnitFormValue {
  return {
    block: unit?.block ?? '',
    number: unit?.number ?? '',
    type: unit?.type ?? 'apartment',
    idealFraction: formatPercentInput(unit?.idealFraction ?? null),
    area: formatAreaInput(unit?.area ?? null),
    parkingSpots: formatParkingSpotsInput(unit?.parkingSpots ?? []),
  };
}

function sameValue(first: UnitFormValue, second: UnitFormValue): boolean {
  return JSON.stringify(trimmed(first)) === JSON.stringify(trimmed(second));
}

function trimmed(value: UnitFormValue): UnitFormValue {
  return {
    block: value.block.trim(),
    number: value.number.trim(),
    type: value.type,
    idealFraction: value.idealFraction.trim(),
    area: value.area.trim(),
    parkingSpots: canonicalSpots(value.parkingSpots),
  };
}

/** Pontuação e espaços à toa não são mudança: compara as vagas já lidas, quando dá para lê-las. */
function canonicalSpots(text: string): string {
  const spots = parseParkingSpots(text);
  return spots.valid ? spots.value.join('|') : text.trim();
}

/** Só chega aqui com o formulário válido: fração e área já foram lidas pelos validadores. */
function toPayload(value: UnitFormValue): UnitPayload {
  const fraction = parsePercent(value.idealFraction);
  const area = parseArea(value.area);
  const block = value.block.trim();
  const spots = parseParkingSpots(value.parkingSpots);
  return {
    block: block === '' ? null : block,
    number: value.number.trim(),
    type: value.type,
    idealFraction:
      fraction.valid && fraction.value !== null ? percentToFraction(fraction.value) : null,
    area: area.valid ? area.value : null,
    parkingSpots: spots.valid ? spots.value : [],
  };
}
