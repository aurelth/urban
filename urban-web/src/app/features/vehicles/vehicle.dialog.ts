import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  computed,
  inject,
  signal,
} from '@angular/core';
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
import { Unit } from '../../core/units/units.models';
import { Vehicle, VehiclePayload } from '../../core/vehicles/vehicles.models';
import { licensePlate } from '../../shared/forms/license-plate.validator';
import { applyMask, unmask } from '../../shared/forms/masks';
import { requiredText } from '../../shared/forms/required-text.validator';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { SelectField, SelectOption } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { unitLabel } from '../units/unit-values';

const MODEL_LIMITS = { min: 2, max: 60 } as const;
const COLOR_LIMITS = { min: 2, max: 30 } as const;

/** O que o diálogo mostra e o que ele faz com o veículo digitado. */
export interface VehicleDialogConfig {
  /** O veículo que está sendo editado; nulo quando é um cadastro novo. */
  readonly vehicle: Vehicle | null;
  /** As unidades ativas, entre as quais se escolhe a do veículo. */
  readonly units: readonly Unit[];
  /** Unidade já escolhida num cadastro novo (a que a lista está filtrando); ignorada se não estiver entre as ativas. */
  readonly defaultUnitId: string | null;
  /**
   * Grava o veículo. Devolve nulo quando deu certo (o diálogo fecha) ou o texto do erro (o diálogo continua aberto, com o
   * que foi digitado, para a pessoa corrigir).
   */
  readonly save: (payload: VehiclePayload) => Promise<string | null>;
}

interface VehicleFormValue {
  unitId: string;
  plate: string;
  model: string;
  color: string;
}

/**
 * Cadastro e edição de um veículo: unidade, placa, modelo e cor. A placa é mostrada em maiúsculas e sem pontuação, e na
 * edição o botão só habilita depois de alguma mudança.
 */
@Component({
  selector: 'urban-vehicle-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    SelectField,
    TextField,
  ],
  templateUrl: './vehicle.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehicleDialog {
  protected readonly config = inject<VehicleDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<VehicleDialog, boolean>>(MatDialogRef);
  protected readonly editing = this.config.vehicle !== null;

  /** As unidades ativas e, na edição, a unidade onde o veículo está, mesmo desativada (o resto do cadastro ainda pode mudar). */
  protected readonly unitOptions: readonly SelectOption[] = toUnitOptions(this.config);

  private readonly initial = toFormValue(this.config);

  protected readonly form = inject(NonNullableFormBuilder).group({
    unitId: [this.initial.unitId, Validators.required],
    plate: [this.initial.plate, [requiredText, licensePlate]],
    model: [this.initial.model, [requiredText, trimmedLength(MODEL_LIMITS.min, MODEL_LIMITS.max)]],
    color: [this.initial.color, [requiredText, trimmedLength(COLOR_LIMITS.min, COLOR_LIMITS.max)]],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);
  protected readonly canSave = computed(() => !this.busy() && (!this.editing || this.changed()));

  protected readonly unitErrors = { required: 'Escolha a unidade' };
  protected readonly plateErrors = {
    required: 'Informe a placa',
    licensePlate: 'Confira a placa: use o formato ABC1234 ou ABC1D23',
  };
  protected readonly modelErrors = {
    required: 'Informe o modelo',
    tooShort: `O modelo precisa ter ao menos ${MODEL_LIMITS.min} caracteres`,
    tooLong: `O modelo pode ter até ${MODEL_LIMITS.max} caracteres`,
  };
  protected readonly colorErrors = {
    required: 'Informe a cor',
    tooShort: `A cor precisa ter ao menos ${COLOR_LIMITS.min} caracteres`,
    tooLong: `A cor pode ter até ${COLOR_LIMITS.max} caracteres`,
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
 * Responde verdadeiro quando o veículo foi gravado e falso quando a pessoa cancelou.
 */
export function openVehicleDialog(
  injector: Injector,
  config: VehicleDialogConfig,
): Promise<boolean> {
  const ref = injector
    .get(MatDialog)
    .open<VehicleDialog, VehicleDialogConfig, boolean>(VehicleDialog, {
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

function toUnitOptions(config: VehicleDialogConfig): readonly SelectOption[] {
  const options = config.units.map((unit) => ({ value: unit.id, label: unitLabel(unit) }));
  const current = config.vehicle;
  if (current !== null && !options.some((option) => option.value === current.unitId)) {
    options.push({
      value: current.unitId,
      label: unitLabel({ block: current.unitBlock, number: current.unitNumber }),
    });
  }
  return options;
}

function toFormValue(config: VehicleDialogConfig): VehicleFormValue {
  if (config.vehicle !== null) {
    const { unitId, plate, model, color } = config.vehicle;
    return { unitId, plate: applyMask('plate', plate), model, color };
  }
  const preselected = config.units.some((unit) => unit.id === config.defaultUnitId);
  return {
    unitId: preselected ? (config.defaultUnitId ?? '') : '',
    plate: '',
    model: '',
    color: '',
  };
}

function sameValue(first: VehicleFormValue, second: VehicleFormValue): boolean {
  return JSON.stringify(toPayload(first)) === JSON.stringify(toPayload(second));
}

/** Só chega à API com o formulário válido; a comparação de mudança usa a mesma forma limpa. */
function toPayload(value: VehicleFormValue): VehiclePayload {
  return {
    unitId: value.unitId,
    plate: unmask('plate', value.plate),
    model: value.model.trim(),
    color: value.color.trim(),
  };
}
