import { Component, ElementRef, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatCheckbox } from '@angular/material/checkbox';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { CondominiumApi } from '../../../core/condominium/condominium.api';
import { toApiProblem } from '../../../core/http/api-problem';
import { describeProblem } from '../../../core/http/problem-message';
import { addressGroup } from '../../../shared/forms/address.group';
import { birthDate } from '../../../shared/forms/birth-date.validator';
import { cnpjNumber } from '../../../shared/forms/cnpj.validator';
import { cpfNumber } from '../../../shared/forms/cpf.validator';
import { displayName } from '../../../shared/forms/display-name.validator';
import { focusFirstInvalidInput } from '../../../shared/forms/focus-first-invalid';
import { fullName } from '../../../shared/forms/full-name.validator';
import { brazilianMobile } from '../../../shared/forms/phone.validator';
import { requiredText } from '../../../shared/forms/required-text.validator';
import { AddressFields } from '../../../shared/ui/address-fields/address.fields';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';
import { TextField } from '../../../shared/ui/text-field/text.field';
import {
  MANAGER_ALREADY_REGISTERED,
  REGISTER_CONDOMINIUM_MESSAGES,
} from './register-condominium.messages';
import { toRegisterPayload } from './register-condominium.payload';

const CONDOMINIUM_NAME_MIN_LENGTH = 2;
const CONDOMINIUM_NAME_MAX_LENGTH = 120;

/**
 * Cadastro do condomínio e do síndico, a primeira tela do síndico recém-chegado. Depois que a API
 * aceita, a sessão é renovada para o token novo trazer o condomínio, e a pessoa vai ao painel.
 */
@Component({
  selector: 'urban-register-condominium-page',
  imports: [
    ReactiveFormsModule,
    MatCard,
    MatCardContent,
    MatCheckbox,
    TextField,
    AddressFields,
    FormSubmit,
  ],
  templateUrl: './register-condominium.page.html',
})
export class RegisterCondominiumPage {
  private readonly api = inject(CondominiumApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly builder = inject(NonNullableFormBuilder);

  protected readonly form = this.builder.group({
    manager: this.builder.group({
      fullName: ['', [requiredText, fullName]],
      displayName: ['', [requiredText, displayName]],
      cpf: ['', [Validators.required, cpfNumber]],
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
      cnpj: ['', [Validators.required, cnpjNumber]],
      address: addressGroup(this.builder),
    }),
  });

  /** Só da tela: não vai para a API, que recebe sempre os dois endereços. */
  protected readonly sameAddress = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

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
  protected readonly cnpjErrors = { required: 'Informe o CNPJ', cnpj: 'Informe um CNPJ válido' };

  /** Com o mesmo endereço, os campos do condomínio somem e deixam de contar na validação. */
  protected useSameAddress(same: boolean): void {
    this.sameAddress.set(same);
    const address = this.form.controls.condominium.controls.address;
    if (same) {
      address.disable();
    } else {
      address.enable();
    }
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.api.register(toRegisterPayload(this.form.getRawValue(), this.sameAddress())),
      );
      await this.goToDashboard();
    } catch (error) {
      const problem = toApiProblem(error);
      if (problem.code === MANAGER_ALREADY_REGISTERED) {
        await this.goToDashboard();
      } else {
        this.error.set(describeProblem(problem, REGISTER_CONDOMINIUM_MESSAGES));
      }
    } finally {
      this.submitting.set(false);
    }
  }

  /** O token em uso não traz o condomínio novo: renova a sessão antes de seguir. */
  private async goToDashboard(): Promise<void> {
    if (await this.session.reload()) {
      await this.router.navigateByUrl('/dashboard');
    }
  }
}
