import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
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
import { formatDecimalInput, parseDecimal } from '../../core/finance/decimal';
import {
  ConsumptionTariff,
  ConsumptionTariffPayload,
  GasTariffValues,
  TariffBand,
} from '../../core/finance/finance.models';
import { formatMoney, formatMoneyInput } from '../../core/finance/money';
import { addMonths, monthOptions } from '../../core/finance/month';
import { SelectField } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { BandsEditor } from './bands.editor';
import { openFinanceDialog } from './finance.dialog';
import {
  bandArray,
  copyBands,
  decimalField,
  gasPricePerCubicMeter,
  syncLimits,
  toBands,
} from './tariff.form';
import { monthName } from './tariff-values';

const MONTHS_BACK = 24;
const MONTHS_AHEAD = 12;
const DIALOG_WIDTH = '720px';
const DEFAULT_SEWAGE_PERCENT = 100;
const SEWAGE_PERCENT_DECIMALS = 2;
const KILOS_DECIMALS = 3;
const PRICE_PER_KILO_DECIMALS = 2;

export interface TariffDialogConfig {
  /** A tarifa que está sendo editada; nula quando é uma tarifa nova. */
  readonly tariff: ConsumptionTariff | null;
  /** Os meses que já têm tarifa: uma tarifa nova não pode começar num deles. */
  readonly takenMonths: readonly string[];
  /** O mês de hoje ("AAAA-MM"), que serve de sugestão para o início da vigência. */
  readonly month: string;
  /** Grava a tarifa do mês. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (month: string, payload: ConsumptionTariffPayload) => Promise<string | null>;
}

/**
 * Cadastra ou edita a tarifa que vale a partir de um mês: faixas de água, esgoto opcional (faixas e percentual cobrado) e gás
 * opcional (kg por m³ e preço do kg). Repetir um mês substitui a tarifa dele, então uma tarifa nova não aceita um mês que já tem.
 */
@Component({
  selector: 'urban-tariff-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCheckbox,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    BandsEditor,
    SelectField,
    TextField,
  ],
  templateUrl: './tariff.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TariffDialog {
  protected readonly config = inject<TariffDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<TariffDialog, boolean>>(MatDialogRef);
  protected readonly editing = this.config.tariff !== null;
  protected readonly editedMonth = monthName(this.config.tariff?.validFrom ?? this.config.month);

  protected readonly months = monthOptions(
    addMonths(this.config.month, -MONTHS_BACK),
    addMonths(this.config.month, MONTHS_AHEAD),
  );

  private readonly taken = new Set(this.config.takenMonths);

  protected readonly form = inject(NonNullableFormBuilder).group({
    month: [
      this.config.tariff?.validFrom ?? this.config.month,
      [
        (control: AbstractControl<string>): ValidationErrors | null =>
          !this.editing && this.taken.has(control.value) ? { taken: true } : null,
      ],
    ],
    water: bandArray(this.config.tariff?.water ?? null),
    sewageEnabled: [this.config.tariff?.sewage != null],
    sewage: bandArray(this.config.tariff?.sewage ?? null),
    sewagePercent: [
      formatDecimalInput(this.config.tariff?.sewagePercent ?? DEFAULT_SEWAGE_PERCENT),
      [decimalField({ decimals: SEWAGE_PERCENT_DECIMALS, max: 100 })],
    ],
    gasEnabled: [this.config.tariff?.gas != null],
    kilos: [
      this.config.tariff?.gas ? formatDecimalInput(this.config.tariff.gas.kilosPerCubicMeter) : '',
      [decimalField({ decimals: KILOS_DECIMALS, max: 100, positive: true })],
    ],
    pricePerKilo: [
      this.config.tariff?.gas ? formatMoneyInput(this.config.tariff.gas.pricePerKilo) : '',
      [decimalField({ decimals: PRICE_PER_KILO_DECIMALS, max: 999.99, positive: true })],
    ],
  });

  protected readonly sewageOn = signal(this.form.controls.sewageEnabled.value);
  protected readonly gasOn = signal(this.form.controls.gasEnabled.value);
  protected readonly gasPrice = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly money = formatMoney;

  protected readonly monthErrors = {
    taken: 'Já existe uma tarifa que começa neste mês. Edite a tarifa dela na lista',
  };
  protected readonly sewagePercentErrors = {
    decimal: 'O percentual vai de 0 a 100, com até 2 casas',
  };
  protected readonly kilosErrors = {
    decimal: 'Informe os kg por m³, maior que zero, com até 3 casas',
  };
  protected readonly pricePerKiloErrors = {
    decimal: 'Informe o preço do kg, maior que zero, com até 2 casas',
  };

  constructor() {
    this.applySewage(this.sewageOn());
    this.applyGas(this.gasOn());
    this.updateGasPrice();

    this.form.controls.sewageEnabled.valueChanges.pipe(takeUntilDestroyed()).subscribe((on) => {
      this.applySewage(on);
    });
    this.form.controls.gasEnabled.valueChanges.pipe(takeUntilDestroyed()).subscribe((on) => {
      this.applyGas(on);
    });
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.updateGasPrice();
    });
  }

  protected copyWaterBands(): void {
    copyBands(this.form.controls.sewage, this.form.controls.water);
  }

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }
    const payload = this.form.valid ? this.payload() : null;
    if (payload === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(this.form.getRawValue().month, payload);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }

  /** O que vai para a API, ou nulo se algum campo ligado não for um número válido (o formulário já avisa). */
  private payload(): ConsumptionTariffPayload | null {
    const raw = this.form.getRawValue();
    const water = toBands(this.form.controls.water);
    if (water === null) {
      return null;
    }

    let sewage: TariffBand[] | null = null;
    let sewagePercent = DEFAULT_SEWAGE_PERCENT;
    if (raw.sewageEnabled) {
      const bands = toBands(this.form.controls.sewage);
      const percent = parseDecimal(raw.sewagePercent, SEWAGE_PERCENT_DECIMALS);
      if (bands === null || percent === null) {
        return null;
      }
      sewage = bands;
      sewagePercent = percent;
    }

    let gas: GasTariffValues | null = null;
    if (raw.gasEnabled) {
      const kilosPerCubicMeter = parseDecimal(raw.kilos, KILOS_DECIMALS);
      const pricePerKilo = parseDecimal(raw.pricePerKilo, PRICE_PER_KILO_DECIMALS);
      if (kilosPerCubicMeter === null || pricePerKilo === null) {
        return null;
      }
      gas = { kilosPerCubicMeter, pricePerKilo };
    }

    return { water, sewage, sewagePercent, gas };
  }

  private applySewage(on: boolean): void {
    this.sewageOn.set(on);
    const { sewage, sewagePercent } = this.form.controls;
    if (on) {
      sewage.enable();
      syncLimits(sewage);
      sewagePercent.enable();
    } else {
      sewage.disable();
      sewagePercent.disable();
    }
  }

  private applyGas(on: boolean): void {
    this.gasOn.set(on);
    const { kilos, pricePerKilo } = this.form.controls;
    if (on) {
      kilos.enable();
      pricePerKilo.enable();
    } else {
      kilos.disable();
      pricePerKilo.disable();
    }
  }

  private updateGasPrice(): void {
    const { gasEnabled, kilos, pricePerKilo } = this.form.getRawValue();
    const kilosValue = parseDecimal(kilos, KILOS_DECIMALS);
    const priceValue = parseDecimal(pricePerKilo, PRICE_PER_KILO_DECIMALS);
    const ready =
      gasEnabled && kilosValue !== null && kilosValue > 0 && priceValue !== null && priceValue > 0;
    this.gasPrice.set(ready ? gasPricePerCubicMeter(kilosValue, priceValue) : null);
  }
}

export function openTariffDialog(injector: Injector, config: TariffDialogConfig): Promise<boolean> {
  return openFinanceDialog(injector, TariffDialog, config, DIALOG_WIDTH);
}
