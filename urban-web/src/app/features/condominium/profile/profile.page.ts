import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  Injector,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { SessionStore } from '../../../core/auth/session.store';
import { MyCondominium } from '../../../core/condominium/condominium.models';
import { ApiProblem } from '../../../core/http/api-problem';
import { describeProblem } from '../../../core/http/problem-message';
import { addressGroup } from '../../../shared/forms/address.group';
import { birthDate } from '../../../shared/forms/birth-date.validator';
import { cpfNumber } from '../../../shared/forms/cpf.validator';
import { displayName } from '../../../shared/forms/display-name.validator';
import { focusFirstInvalidInput } from '../../../shared/forms/focus-first-invalid';
import { fullName } from '../../../shared/forms/full-name.validator';
import { brazilianMobile } from '../../../shared/forms/phone.validator';
import { requiredText } from '../../../shared/forms/required-text.validator';
import { AddressFields } from '../../../shared/ui/address-fields/address.fields';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';
import { openPasswordConfirm } from '../../../shared/ui/password-confirm-dialog/password-confirm.dialog';
import { TextField } from '../../../shared/ui/text-field/text.field';
import { PROFILE_MESSAGES } from './profile.messages';
import {
  ProfileFormValue,
  freeFieldsChanged,
  identityChanged,
  toFreePayload,
  toIdentityPayload,
  toProfileFormValue,
} from './profile.payload';
import { ProfileStore } from './profile.store';

const CONDOMINIUM_NAME_MIN_LENGTH = 2;
const CONDOMINIUM_NAME_MAX_LENGTH = 120;

/**
 * "Meus dados": o síndico vê e corrige o próprio cadastro e o do condomínio. Os campos livres (nome de exibição,
 * celular, endereços e nome do condomínio) salvam direto. Nome completo, data de nascimento e CPF pedem a senha da
 * conta num diálogo, e o CPF só vira campo quando a pessoa pede para trocá-lo: o número salvo nunca chega à tela.
 * O CNPJ não muda. A versão lida junto com os dados volta no envio, e a API recusa (409) se outra pessoa mexeu antes.
 */
@Component({
  selector: 'urban-profile-page',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    TextField,
    AddressFields,
    FormSubmit,
  ],
  templateUrl: './profile.page.html',
  providers: [ProfileStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage implements OnInit {
  protected readonly store = inject(ProfileStore);
  private readonly session = inject(SessionStore);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly builder = inject(NonNullableFormBuilder);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = this.builder.group({
    manager: this.builder.group({
      fullName: ['', [requiredText, fullName]],
      displayName: ['', [requiredText, displayName]],
      // Existe sempre, mas desabilitado (fora da validação) até a pessoa pedir para trocar o CPF.
      cpf: this.builder.control({ value: '', disabled: true }, [Validators.required, cpfNumber]),
      birthDate: ['', [Validators.required, birthDate()]],
      phone: ['', [Validators.required, brazilianMobile]],
      address: addressGroup(this.builder),
    }),
    condominium: this.builder.group({
      name: [
        '',
        [
          requiredText,
          Validators.minLength(CONDOMINIUM_NAME_MIN_LENGTH),
          Validators.maxLength(CONDOMINIUM_NAME_MAX_LENGTH),
        ],
      ],
      address: addressGroup(this.builder),
    }),
  });

  protected readonly changingCpf = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

  private readonly current = signal<ProfileFormValue | null>(null);

  protected readonly original = computed<ProfileFormValue | null>(() => {
    const data = this.store.data();
    return data === null ? null : toProfileFormValue(data);
  });

  protected readonly hasChanges = computed(() => {
    const original = this.original();
    const current = this.current();
    return (
      original !== null &&
      current !== null &&
      (freeFieldsChanged(original, current) || identityChanged(original, current))
    );
  });

  protected readonly saveDisabled = computed(
    () => !this.hasChanges() || this.store.status() === 'conflict',
  );
  protected readonly busy = computed(() => this.store.status() === 'saving');

  protected readonly fullNameErrors = {
    required: 'Informe o nome completo',
    fullName: 'Informe o nome e o sobrenome',
  };
  protected readonly displayNameErrors = {
    required: 'Informe como prefere ser chamado',
    displayName: 'Use de 2 a 40 letras, sem números',
  };
  protected readonly cpfErrors = { required: 'Informe o CPF', cpf: 'Informe um CPF válido' };
  protected readonly birthDateErrors = {
    required: 'Informe a data de nascimento',
    underAge: 'O síndico precisa ter 18 anos ou mais',
    date: 'Informe uma data válida, como 31/01/1980',
  };
  protected readonly phoneErrors = {
    required: 'Informe o celular',
    mobile: 'Informe um celular com DDD',
  };
  protected readonly nameErrors = {
    required: 'Informe o nome do condomínio',
    minlength: `O nome deve ter de ${CONDOMINIUM_NAME_MIN_LENGTH} a ${CONDOMINIUM_NAME_MAX_LENGTH} caracteres`,
    maxlength: `O nome deve ter de ${CONDOMINIUM_NAME_MIN_LENGTH} a ${CONDOMINIUM_NAME_MAX_LENGTH} caracteres`,
  };

  ngOnInit(): void {
    this.form.valueChanges.subscribe(() => {
      this.current.set(this.form.getRawValue());
      this.saved.set(false);
    });
    void this.load();
  }

  /** Busca os dados e, quando chegam, troca o formulário por eles. Serve a abertura, a nova tentativa e o recarregar. */
  protected async load(): Promise<void> {
    this.error.set(null);
    this.saved.set(false);
    await this.store.load();
    const data = this.store.data();
    if (data !== null) {
      this.fill(data);
    }
  }

  protected changeCpf(): void {
    this.changingCpf.set(true);
    this.form.controls.manager.controls.cpf.enable();
  }

  protected keepCpf(): void {
    this.stopChangingCpf();
    this.current.set(this.form.getRawValue());
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // O foco procura a marca de campo inválido, que só chega ao HTML depois de a tela se atualizar.
      this.changeDetector.detectChanges();
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }
    const original = this.original();
    const data = this.store.data();
    if (original === null || data === null || this.saveDisabled()) {
      return;
    }

    this.error.set(null);
    this.saved.set(false);
    const value = this.form.getRawValue();
    const free = freeFieldsChanged(original, value);

    let finished: boolean;
    if (identityChanged(original, value)) {
      finished = await this.saveWithPassword(value, data.version, free);
    } else {
      finished = await this.saveFreeFields(value, data.version);
    }
    if (finished) {
      await this.finish();
    }
  }

  /**
   * Os dados sensíveis vão primeiro, dentro do diálogo da senha; os livres vão depois, com a versão que a gravação
   * anterior devolveu. Erro da senha ou do dado sensível fica no diálogo; os demais, na tela.
   */
  private async saveWithPassword(
    value: ProfileFormValue,
    version: string,
    alsoFree: boolean,
  ): Promise<boolean> {
    let completed = false;
    await openPasswordConfirm(this.injector, {
      title: 'Confirme com a sua senha',
      description:
        'Para alterar o nome completo, a data de nascimento ou o CPF, informe a senha da sua conta',
      confirmLabel: 'Confirmar e salvar',
      busyLabel: 'Salvando',
      run: async (password): Promise<string | null> => {
        const problem = await this.store.saveIdentity(toIdentityPayload(value, version, password));
        if (problem !== null) {
          return this.isFatal(problem) ? null : describeProblem(problem, PROFILE_MESSAGES);
        }
        completed = alsoFree ? await this.saveFreeFields(value, this.store.data()!.version) : true;
        return null;
      },
    });
    return completed;
  }

  private async saveFreeFields(value: ProfileFormValue, version: string): Promise<boolean> {
    const problem = await this.store.saveFree(toFreePayload(value, version));
    if (problem !== null) {
      // Conflito e cadastro ausente têm aviso próprio na tela; o resto é texto no fim do formulário.
      if (!this.isFatal(problem)) {
        this.error.set(describeProblem(problem, PROFILE_MESSAGES));
      }
      return false;
    }
    return true;
  }

  /** Conflito de versão e cadastro ausente: a tela mostra um aviso próprio e o diálogo fecha. */
  private isFatal(problem: ApiProblem): boolean {
    const status = this.store.status();
    return status === 'conflict' || status === 'not-found' || problem.status === 404;
  }

  /** Tudo gravado: renova a sessão (o cabeçalho mostra o nome de exibição novo), avisa e recomeça do que foi salvo. */
  private async finish(): Promise<void> {
    const data = this.store.data();
    if (data !== null) {
      this.fill(data);
    }
    this.saved.set(true);
    await this.session.reload();
  }

  private fill(data: MyCondominium): void {
    this.stopChangingCpf();
    this.form.reset(toProfileFormValue(data), { emitEvent: false });
    this.form.controls.manager.controls.cpf.disable({ emitEvent: false });
    this.current.set(this.form.getRawValue());
  }

  private stopChangingCpf(): void {
    this.changingCpf.set(false);
    const cpf = this.form.controls.manager.controls.cpf;
    cpf.reset('', { emitEvent: false });
    cpf.disable({ emitEvent: false });
  }
}
