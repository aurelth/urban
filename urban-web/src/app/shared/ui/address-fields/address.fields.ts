import { Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlContainer, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { EMPTY, distinctUntilChanged, map, switchMap, tap } from 'rxjs';
import { PostalCodeApi } from '../../../core/postal-code/postal-code.api';
import { PostalCodeLookup } from '../../../core/postal-code/postal-code.models';
import { addressPatch } from '../../forms/address-autofill';
import { ADDRESS_MAX_LENGTH } from '../../forms/address.group';
import { unmask } from '../../forms/masks';
import { USE_PARENT_FORM } from '../form-field.base';
import { StateField } from '../state-field/state.field';
import { TextField } from '../text-field/text.field';

type LookupState = 'idle' | 'loading' | PostalCodeLookup['status'];

const NOTICES: Readonly<Record<LookupState, string | null>> = {
  idle: null,
  loading: 'Buscando o endereço',
  found: 'Endereço preenchido. Confira os dados e informe o número',
  'not-found': 'Não encontramos esse CEP. Preencha o endereço manualmente',
  unavailable: 'Não foi possível buscar o endereço agora. Preencha manualmente',
};

/**
 * Os sete campos de um endereço, dentro do grupo `groupName` do formulário em que for colocado
 * (veja `addressGroup`). `personal` diz se é o endereço da própria pessoa, caso em que o navegador
 * pode sugerir o endereço salvo; num endereço de outro lugar, as sugestões ficam desligadas.
 *
 * Quando o CEP fica completo e válido, consulta a API e troca rua, bairro, cidade e UF pelo que ele informa;
 * número e complemento são sempre da pessoa. Se a consulta não der, o aviso diz para preencher à mão e nada
 * é travado. Uma consulta em andamento é descartada se a pessoa trocar o CEP antes da resposta.
 */
@Component({
  selector: 'urban-address-fields',
  imports: [ReactiveFormsModule, TextField, StateField],
  templateUrl: './address.fields.html',
  viewProviders: [USE_PARENT_FORM],
})
export class AddressFields implements OnInit {
  readonly groupName = input.required<string>();
  readonly personal = input(true);

  private readonly container = inject(ControlContainer);
  private readonly postalCodes = inject(PostalCodeApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly lookupState = signal<LookupState>('idle');

  protected readonly notice = computed(() => NOTICES[this.lookupState()]);

  protected readonly postalCodeErrors = {
    required: 'Informe o CEP',
    postalCode: 'Informe um CEP válido',
  };
  protected readonly streetErrors = {
    required: 'Informe a rua',
    maxlength: `A rua deve ter até ${ADDRESS_MAX_LENGTH.street} caracteres`,
  };
  protected readonly numberErrors = {
    required: 'Informe o número (use S/N quando não houver)',
    maxlength: `O número deve ter até ${ADDRESS_MAX_LENGTH.number} caracteres`,
  };
  protected readonly complementErrors = {
    maxlength: `O complemento deve ter até ${ADDRESS_MAX_LENGTH.complement} caracteres`,
  };
  protected readonly neighborhoodErrors = {
    required: 'Informe o bairro',
    maxlength: `O bairro deve ter até ${ADDRESS_MAX_LENGTH.neighborhood} caracteres`,
  };
  protected readonly cityErrors = {
    required: 'Informe a cidade',
    maxlength: `A cidade deve ter até ${ADDRESS_MAX_LENGTH.city} caracteres`,
  };

  ngOnInit(): void {
    const group = this.container.control?.get(this.groupName()) as FormGroup | null | undefined;
    const postalCode = group?.get('postalCode');
    if (!group || !postalCode) {
      return;
    }
    postalCode.valueChanges
      .pipe(
        map((): string => (postalCode.valid ? unmask('cep', String(postalCode.value)) : '')),
        distinctUntilChanged(),
        tap((digits) => this.lookupState.set(digits === '' ? 'idle' : 'loading')),
        switchMap((digits) => (digits === '' ? EMPTY : this.postalCodes.lookup(digits))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.show(group, result));
  }

  private show(group: FormGroup, result: PostalCodeLookup): void {
    if (result.status === 'found') {
      group.patchValue(addressPatch(result.address));
    }
    this.lookupState.set(result.status);
  }

  protected suggest(token: string): string {
    return this.personal() ? token : 'off';
  }
}
