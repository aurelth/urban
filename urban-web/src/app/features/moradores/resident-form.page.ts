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
import { birthDate, isMinorBirth } from '../../shared/forms/birth-date.validator';
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
import { minorKind, unlessMinor } from './resident-minor';
import {
  ResidentFormValue,
  residentChanged,
  toCreatePayload,
  toResidentFormValue,
  toUpdatePayload,
} from './resident-form.payload';
import { ResidentAccess } from './resident-access';
import { EMAIL_LOCKED_MESSAGE, RESIDENT_FORM_MESSAGES } from './resident-form.messages';
import { ResidentFormStore } from './resident-form.store';
import { HOUSING_ERRORS, KIND_OPTIONS } from './resident-housing.fields';
import { ResidentOccupancies } from './resident-occupancies';
import { occupancyUnit } from './resident-values';

const CREATE_EMAIL_HINT =
  'Opcional. Sem e-mail o morador fica cadastrado, mas não pode ser convidado a acessar o sistema';
const INVITED_EMAIL_HINT =
  'O convite já foi enviado a este endereço. Para corrigi-lo, salve e use Reenviar convite';
const UNSAVED_MESSAGE = 'Salve ou descarte as alterações antes de convidar';

const PROFESSION_LENGTH = { min: 2, max: 80 };
const RELATIONSHIP_LENGTH = { min: 2, max: 40 };

/**
 * Cadastro (`/moradores/novo`) e edição (`/moradores/:id/editar`) de um morador, na mesma tela: o `id` da rota diz qual.
 * O cadastro pede os dados pessoais, o contato de emergência e a moradia (unidade, tipo de vínculo e entrada) e termina
 * numa confirmação. A edição troca só os dados pessoais, com a versão lida (a API recusa com 409 se outra pessoa mexeu
 * antes), e o CPF só vira campo quando a pessoa pede para trocá-lo: o número salvo nunca chega à tela. O vínculo com a
 * unidade (nova moradia, saída) tem o seu próprio componente, `urban-resident-occupancies`, abaixo do formulário.
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
    ResidentAccess,
    ResidentOccupancies,
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

  /** Quem tem menos de 18 anos é dependente e fica dispensado de profissão e de contato de emergência. */
  protected readonly isMinor = signal(false);

  protected readonly form = this.builder.group({
    fullName: ['', [requiredText, fullName]],
    // Na edição fica desabilitado (fora da validação) até a pessoa pedir para trocar o CPF.
    cpf: ['', [Validators.required, cpfNumber]],
    birthDate: ['', [Validators.required, birthDate(undefined, { allowMinor: true })]],
    phone: ['', [Validators.required, brazilianPhone]],
    email: ['', [emailFormat]],
    profession: [
      '',
      [
        unlessMinor(
          () => this.isMinor(),
          Validators.compose([
            requiredText,
            trimmedLength(PROFESSION_LENGTH.min, PROFESSION_LENGTH.max),
          ])!,
        ),
      ],
    ],
    contactName: [
      '',
      [unlessMinor(() => this.contactMayBeBlank(), Validators.compose([requiredText, fullName])!)],
    ],
    contactRelationship: [
      '',
      [
        unlessMinor(
          () => this.contactMayBeBlank(),
          Validators.compose([
            requiredText,
            trimmedLength(RELATIONSHIP_LENGTH.min, RELATIONSHIP_LENGTH.max),
          ])!,
        ),
      ],
    ],
    contactPhone: [
      '',
      [
        unlessMinor(
          () => this.contactMayBeBlank(),
          Validators.compose([Validators.required, brazilianPhone])!,
        ),
      ],
    ],
    // Os três últimos só valem no cadastro: na edição ficam desabilitados.
    unitId: ['', [Validators.required]],
    kind: this.builder.control<OccupancyKind | ''>('', [
      Validators.required,
      minorKind(() => this.isMinor()),
    ]),
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

  /** A dica do e-mail segue o acesso: livre sem convite, corrigível com o convite enviado e travado depois da senha. */
  protected readonly emailHint = computed(() => {
    switch (this.store.resident()?.accessStatus) {
      case 'active':
      case 'inactive':
        return EMAIL_LOCKED_MESSAGE;
      case 'invited':
        return INVITED_EMAIL_HINT;
      default:
        return CREATE_EMAIL_HINT;
    }
  });

  /** Convidar lê o morador gravado, então com dados por salvar o botão espera (senão o convite iria para o e-mail antigo). */
  protected readonly inviteBlockedReason = computed(() => {
    const original = this.original();
    const current = this.current();
    return original !== null && current !== null && residentChanged(original, current)
      ? UNSAVED_MESSAGE
      : null;
  });

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
  protected readonly unitErrors = HOUSING_ERRORS.unit;
  protected readonly kindErrors = HOUSING_ERRORS.kind;
  protected readonly startDateErrors = HOUSING_ERRORS.startDate;

  ngOnInit(): void {
    if (this.isEditing()) {
      this.disableCreateOnlyFields();
    }
    this.form.valueChanges.subscribe(() => {
      this.syncMinor();
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

  /** O convite saiu: o morador ganhou a conta, então lê de novo (acesso e versão) sem apagar o que a pessoa já digitou. */
  protected async onInvited(): Promise<void> {
    const id = this.id();
    if (id === undefined) {
      return;
    }
    const before = this.current();
    await this.store.refreshResident(id);
    const fresh = this.store.resident();
    if (fresh === null) {
      return;
    }
    if (this.current() === before) {
      this.fill(fresh);
    } else {
      this.applyEmailLock(fresh);
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
    this.syncMinor();
    this.applyEmailLock(resident);
    this.current.set(this.form.getRawValue());
  }

  /** Depois que o morador cria a senha o e-mail é o login dele e não muda mais; o valor continua indo na gravação. */
  private applyEmailLock(resident: Resident): void {
    const locked = resident.accessStatus === 'active' || resident.accessStatus === 'inactive';
    const email = this.form.controls.email;
    if (locked) {
      email.disable({ emitEvent: false });
    } else {
      email.enable({ emitEvent: false });
    }
  }

  /**
   * O menor pode deixar o contato inteiro em branco, mas quem começa a preenchê-lo precisa terminar. As regras de
   * profissão, contato e vínculo dependem da data de nascimento e dos outros campos do contato, então são conferidas de novo
   * a cada mudança.
   */
  private contactMayBeBlank(): boolean {
    if (!this.isMinor()) {
      return false;
    }
    const { contactName, contactRelationship, contactPhone } = this.form.controls;
    return [contactName, contactRelationship, contactPhone].every(
      (control) => control.value.trim() === '',
    );
  }

  private syncMinor(): void {
    const {
      birthDate: born,
      profession,
      contactName,
      contactRelationship,
      contactPhone,
      kind,
    } = this.form.controls;
    this.isMinor.set(isMinorBirth(born.value));
    for (const control of [profession, contactName, contactRelationship, contactPhone, kind]) {
      control.updateValueAndValidity({ emitEvent: false, onlySelf: true });
    }
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
