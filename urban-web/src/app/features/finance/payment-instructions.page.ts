import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import {
  BankAccountKind,
  PaymentInstructions,
} from '../../core/finance/payment-instructions.models';
import { describeProblem } from '../../core/http/problem-message';
import { focusFirstInvalidInput } from '../../shared/forms/focus-first-invalid';
import { FormSubmit } from '../../shared/ui/form-submit/form-submit';
import { SelectField } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { formatRequestedAt } from '../admin/registration-requests/requested-at';
import {
  ACCOUNT_KIND_OPTIONS,
  PIX_KIND_OPTIONS,
  PaymentFormField,
  PaymentFormValue,
  PixKindValue,
  formChanged,
  paymentFormErrors,
  pixKeyHint,
  pixKeyMask,
  toFormValue,
  toPayload,
} from './payment-instructions.form';
import {
  ACCOUNT_KIND_ERRORS,
  ACCOUNT_NUMBER_ERRORS,
  AGENCY_ERRORS,
  BANK_ERRORS,
  PAYMENT_INSTRUCTIONS_MESSAGES,
  PIX_KEY_ERRORS,
} from './payment-instructions.messages';
import { PaymentInstructionsStore } from './payment-instructions.store';

const FIELDS: readonly PaymentFormField[] = [
  'pixKind',
  'pixKey',
  'bank',
  'agency',
  'number',
  'accountKind',
];

/**
 * "Dados para pagamento": a chave Pix e a conta bancária do condomínio, que os moradores usam para pagar a fatura. As
 * duas partes são opcionais e independentes: "Não informar" no tipo da chave tira o Pix, e a conta em branco tira a
 * conta. Uma vez começada, a conta precisa ficar completa. A chave troca de máscara conforme o tipo, então trocar o
 * tipo apaga o que estava digitado. Os problemas de cada campo vêm de `paymentFormErrors`; a API tem a palavra final.
 */
@Component({
  selector: 'urban-payment-instructions-page',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    SelectField,
    TextField,
    FormSubmit,
  ],
  templateUrl: './payment-instructions.page.html',
  providers: [PaymentInstructionsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentInstructionsPage implements OnInit {
  protected readonly store = inject(PaymentInstructionsStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly builder = inject(NonNullableFormBuilder);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = this.builder.group({
    pixKind: this.builder.control<PixKindValue>(''),
    pixKey: [''],
    bank: [''],
    agency: [''],
    number: [''],
    accountKind: this.builder.control<BankAccountKind | ''>(''),
  });

  protected readonly pixKinds = PIX_KIND_OPTIONS;
  protected readonly accountKinds = ACCOUNT_KIND_OPTIONS;
  protected readonly pixKeyErrors = PIX_KEY_ERRORS;
  protected readonly bankErrors = BANK_ERRORS;
  protected readonly agencyErrors = AGENCY_ERRORS;
  protected readonly numberErrors = ACCOUNT_NUMBER_ERRORS;
  protected readonly accountKindErrors = ACCOUNT_KIND_ERRORS;
  protected readonly formatDate = formatRequestedAt;

  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

  private readonly current = signal<PaymentFormValue | null>(null);
  private lastKind: PixKindValue = '';

  protected readonly pixKind = computed(() => this.current()?.pixKind ?? '');
  protected readonly pixMask = computed(() => pixKeyMask(this.pixKind()));
  protected readonly pixHint = computed(() => pixKeyHint(this.pixKind()));

  protected readonly updatedAt = computed(() => this.store.instructions()?.updatedAt ?? null);

  protected readonly hasChanges = computed(() => {
    const current = this.current();
    return current !== null && formChanged(toFormValue(this.store.instructions()), current);
  });

  ngOnInit(): void {
    this.form.valueChanges.subscribe(() => this.onValueChanged());
    void this.load();
  }

  /** Busca os dados e, quando chegam, troca o formulário por eles. Serve à abertura e à nova tentativa. */
  protected async load(): Promise<void> {
    this.error.set(null);
    this.saved.set(false);
    await this.store.load();
    const instructions = this.store.instructions();
    if (instructions !== null) {
      this.fill(instructions);
    }
  }

  protected async submit(): Promise<void> {
    this.applyErrors();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // O foco procura a marca de campo inválido, que só chega ao HTML depois de a tela se atualizar.
      this.changeDetector.detectChanges();
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }
    if (!this.hasChanges()) {
      return;
    }

    this.error.set(null);
    this.saved.set(false);
    const problem = await this.store.save(toPayload(this.form.getRawValue()));
    if (problem !== null) {
      this.error.set(describeProblem(problem, PAYMENT_INSTRUCTIONS_MESSAGES));
      return;
    }
    const instructions = this.store.instructions();
    if (instructions !== null) {
      this.fill(instructions);
    }
    this.saved.set(true);
  }

  private onValueChanged(): void {
    const kind = this.form.controls.pixKind.value;
    if (kind !== this.lastKind) {
      this.lastKind = kind;
      this.form.controls.pixKey.setValue('', { emitEvent: false });
    }
    this.current.set(this.form.getRawValue());
    this.applyErrors();
    this.saved.set(false);
  }

  /** Marca em cada campo o erro que `paymentFormErrors` achou; sem isso o Angular só conhece "obrigatório". */
  private applyErrors(): void {
    const errors = paymentFormErrors(this.form.getRawValue());
    for (const name of FIELDS) {
      const key = errors[name];
      this.form.controls[name].setErrors(key === undefined ? null : { [key]: true });
    }
  }

  private fill(instructions: PaymentInstructions): void {
    const value = toFormValue(instructions);
    this.lastKind = value.pixKind;
    this.form.reset(value, { emitEvent: false });
    this.current.set(this.form.getRawValue());
    this.applyErrors();
  }
}
