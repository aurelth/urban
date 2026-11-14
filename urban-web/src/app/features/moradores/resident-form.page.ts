import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { OccupancyKind, Resident } from '../../core/residents/residents.models';
import { birthDate } from '../../shared/forms/birth-date.validator';
import { cpfNumber } from '../../shared/forms/cpf.validator';
import { emailFormat } from '../../shared/forms/email.validator';
import { focusFirstInvalidInput } from '../../shared/forms/focus-first-invalid';
import { fullName } from '../../shared/forms/full-name.validator';
import { brazilianPhone } from '../../shared/forms/phone.validator';
import { requiredText } from '../../shared/forms/required-text.validator';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { FormSubmit } from '../../shared/ui/form-submit/form-submit';
import { SelectField, SelectOption } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { unitLabel } from '../units/unit-values';
import { entryDate } from './resident-field.validators';
import {
  ResidentFormValue,
  residentChanged,
  toCreatePayload,
  toResidentFormValue,
  toUpdatePayload,
} from './resident-form.payload';
import { RESIDENT_FORM_MESSAGES } from './resident-form.messages';
import { ResidentFormStore } from './resident-form.store';
import { kindLabel, occupancyUnit } from './resident-values';

const PROFESSION_LENGTH = { min: 2, max: 80 };
const RELATIONSHIP_LENGTH = { min: 2, max: 40 };

const KIND_OPTIONS: readonly SelectOption[] = [
  { value: 'owner', label: kindLabel('owner') },
  { value: 'tenant', label: kindLabel('tenant') },
];

/**
 * Cadastro (`/moradores/novo`) e edição (`/moradores/:id/editar`) de um morador, na mesma tela: o `id` da rota diz qual.
 * O cadastro pede os dados pessoais, o contato de emergência e a moradia (unidade, tipo de vínculo e entrada) e termina
 * numa confirmação. A edição troca só os dados pessoais, com a versão lida (a API recusa com 409 se outra pessoa mexeu
 * antes), e o CPF só vira campo quando a pessoa pede para trocá-lo: o número salvo nunca chega à tela. O vínculo com a
 * unidade (nova moradia, saída) é de outra tela.
 */
@Component({
  selector: 'urban-resident-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    TextField,
    SelectField,
    FormSubmit,
  ],
  templateUrl: './resident-form.page.html',
  providers: [ResidentFormStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResidentFormPage implements OnInit {
  /** O id da rota de edição; sem ele a tela é o cadastro. */
  readonly id = input<string>();

  protected readonly store = inject(ResidentFormStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly builder = inject(NonNullableFormBuilder);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = this.builder.group({
    fullName: ['', [requiredText, fullName]],
    // Na edição fica desabilitado (fora da validação) até a pessoa pedir para trocar o CPF.
    cpf: ['', [Validators.required, cpfNumber]],
    birthDate: ['', [Validators.required, birthDate()]],
    phone: ['', [Validators.required, brazilianPhone]],
    email: ['', [emailFormat]],
    profession: ['', [requiredText, trimmedLength(PROFESSION_LENGTH.min, PROFESSION_LENGTH.max)]],
    contactName: ['', [requiredText, fullName]],
    contactRelationship: [
      '',
      [requiredText, trimmedLength(RELATIONSHIP_LENGTH.min, RELATIONSHIP_LENGTH.max)],
    ],
    contactPhone: ['', [Validators.required, brazilianPhone]],
    // Os três últimos só valem no cadastro: na edição ficam desabilitados.
    unitId: ['', [Validators.required]],
    kind: this.builder.control<OccupancyKind | ''>('', [Validators.required]),
    startDate: ['', [Validators.required, entryDate()]],
  });

  protected readonly isEditing = computed(() => this.id() !== undefined);
  protected readonly changingCpf = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

  private readonly current = signal<ResidentFormValue | null>(null);

  protected readonly kindOptions = KIND_OPTIONS;
  protected readonly unitOptions = computed<readonly SelectOption[]>(() =>
    this.store.units().map((unit) => ({ value: unit.id, label: unitLabel(unit) })),
  );

  protected readonly original = computed<ResidentFormValue | null>(() => {
    const resident = this.store.resident();
    return resident === null ? null : toResidentFormValue(resident);
  });

  protected readonly saveDisabled = computed(() => {
    if (!this.isEditing()) {
      return false;
    }
    const original = this.original();
    const current = this.current();
    return (
      original === null ||
      current === null ||
      !residentChanged(original, current) ||
      this.store.status() === 'conflict'
    );
  });
  protected readonly busy = computed(() => this.store.status() === 'saving');

  protected readonly createdUnit = computed(() => {
    const occupancy = this.store.created()?.occupancies[0];
    return occupancy === undefined ? null : occupancyUnit(occupancy);
  });

  protected readonly fullNameErrors = {
    required: 'Informe o nome completo',
    fullName: 'Informe o nome e o sobrenome',
  };
  protected readonly cpfErrors = { required: 'Informe o CPF', cpf: 'Informe um CPF válido' };
  protected readonly birthDateErrors = {
    required: 'Informe a data de nascimento',
    underAge: 'O morador precisa ter 18 anos ou mais',
    date: 'Informe uma data válida, como 31/01/1980',
  };
  protected readonly phoneErrors = {
    required: 'Informe o telefone',
    phone: 'Informe um telefone com DDD',
  };
  protected readonly emailErrors = { email: 'Informe um e-mail válido, como nome@exemplo.com' };
  protected readonly professionErrors = {
    required: 'Informe a profissão',
    tooShort: `A profissão deve ter de ${PROFESSION_LENGTH.min} a ${PROFESSION_LENGTH.max} caracteres`,
    tooLong: `A profissão deve ter de ${PROFESSION_LENGTH.min} a ${PROFESSION_LENGTH.max} caracteres`,
  };
  protected readonly contactNameErrors = {
    required: 'Informe o nome do contato',
    fullName: 'Informe o nome e o sobrenome do contato',
  };
  protected readonly relationshipErrors = {
    required: 'Informe o parentesco',
    tooShort: `O parentesco deve ter de ${RELATIONSHIP_LENGTH.min} a ${RELATIONSHIP_LENGTH.max} caracteres`,
    tooLong: `O parentesco deve ter de ${RELATIONSHIP_LENGTH.min} a ${RELATIONSHIP_LENGTH.max} caracteres`,
  };
  protected readonly contactPhoneErrors = {
    required: 'Informe o telefone do contato',
    phone: 'Informe um telefone com DDD',
  };
  protected readonly unitErrors = { required: 'Escolha a unidade' };
  protected readonly kindErrors = { required: 'Escolha se é proprietário ou inquilino' };
  protected readonly startDateErrors = {
    required: 'Informe a data de entrada',
    date: 'Informe uma data de entrada válida, como 01/03/2024',
  };

  ngOnInit(): void {
    if (this.isEditing()) {
      this.disableCreateOnlyFields();
    }
    this.form.valueChanges.subscribe(() => {
      this.current.set(this.form.getRawValue());
      this.saved.set(false);
    });
    void this.load();
  }

  /** Busca o que a tela precisa e, na edição, troca o formulário pelos dados lidos. Serve à abertura, a tentar de novo e a recarregar. */
  protected async load(): Promise<void> {
    this.error.set(null);
    this.saved.set(false);
    const id = this.id();
    if (id === undefined) {
      await this.store.loadForCreate();
      return;
    }
    await this.store.loadForEdit(id);
    const resident = this.store.resident();
    if (resident !== null) {
      this.fill(resident);
    }
  }

  protected changeCpf(): void {
    this.changingCpf.set(true);
    this.form.controls.cpf.enable();
  }

  protected keepCpf(): void {
    this.stopChangingCpf();
    this.current.set(this.form.getRawValue());
  }

  protected startAnother(): void {
    this.store.startAnother();
    this.error.set(null);
    this.form.reset();
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // O foco procura a marca de campo inválido, que só chega ao HTML depois de a tela se atualizar.
      this.changeDetector.detectChanges();
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }
    if (this.saveDisabled() || this.busy()) {
      return;
    }

    this.error.set(null);
    this.saved.set(false);
    const value = this.form.getRawValue();
    const id = this.id();
    const resident = this.store.resident();
    if (id === undefined) {
      this.showProblem(await this.store.create(toCreatePayload(value)));
    } else if (resident !== null) {
      const problem = await this.store.update(id, toUpdatePayload(value, resident.version));
      this.showProblem(problem);
      if (problem === null) {
        this.fill(this.store.resident()!);
        this.saved.set(true);
      }
    }
  }

  /** Conflito de versão e morador ausente têm aviso próprio na tela; o resto é texto no fim do formulário. */
  private showProblem(problem: ApiProblem | null): void {
    const status = this.store.status();
    if (problem !== null && status !== 'conflict' && status !== 'not-found') {
      this.error.set(describeProblem(problem, RESIDENT_FORM_MESSAGES));
    }
  }

  private fill(resident: Resident): void {
    this.stopChangingCpf();
    this.form.reset(toResidentFormValue(resident), { emitEvent: false });
    this.current.set(this.form.getRawValue());
  }

  private stopChangingCpf(): void {
    this.changingCpf.set(false);
    if (this.isEditing()) {
      this.form.controls.cpf.reset('', { emitEvent: false });
      this.form.controls.cpf.disable({ emitEvent: false });
    }
  }

  private disableCreateOnlyFields(): void {
    const { cpf, unitId, kind, startDate } = this.form.controls;
    for (const control of [cpf, unitId, kind, startDate]) {
      control.disable({ emitEvent: false });
    }
  }
}
