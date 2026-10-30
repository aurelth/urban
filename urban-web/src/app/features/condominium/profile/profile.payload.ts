import {
  MyCondominium,
  UpdateManagerIdentityPayload,
  UpdateMyCondominiumPayload,
} from '../../../core/condominium/condominium.models';
import {
  AddressFormValue,
  cleanText,
  toAddressPayload,
} from '../../../shared/forms/address.payload';
import { applyMask, unmask } from '../../../shared/forms/masks';

/**
 * O que o formulário de "Meus dados" guarda, com os textos como aparecem nos campos. O CPF só existe aqui quando a pessoa
 * decide trocá-lo: o número que está salvo nunca chega à tela, só a versão mascarada, que fica fora do formulário.
 */
export interface ProfileFormValue {
  manager: {
    fullName: string;
    displayName: string;
    cpf: string;
    birthDate: string;
    phone: string;
    address: AddressFormValue;
  };
  condominium: {
    name: string;
    address: AddressFormValue;
  };
}

/** Dos dados da API para os campos: com as máscaras, a data em dd/mm/aaaa e o CPF em branco. */
export function toProfileFormValue(mine: MyCondominium): ProfileFormValue {
  const { manager, condominium } = mine;
  return {
    manager: {
      fullName: manager.fullName,
      displayName: mine.displayName ?? '',
      cpf: '',
      birthDate: toDisplayDate(manager.birthDate),
      phone: applyMask('phone', manager.phone),
      address: toFormAddress(manager.address),
    },
    condominium: {
      name: condominium.name,
      address: toFormAddress(condominium.address),
    },
  };
}

/** Corpo da edição dos campos livres. O que não pode mudar por aqui (CNPJ, CPF, nome completo, nascimento) não vai. */
export function toFreePayload(
  value: ProfileFormValue,
  version: string,
): UpdateMyCondominiumPayload {
  return {
    version,
    displayName: cleanText(value.manager.displayName),
    phone: unmask('phone', value.manager.phone),
    managerAddress: toAddressPayload(value.manager.address),
    condominiumName: cleanText(value.condominium.name),
    condominiumAddress: toAddressPayload(value.condominium.address),
  };
}

/** Corpo da edição dos dados sensíveis. Sem CPF novo, a chave nem existe e a API mantém o atual. */
export function toIdentityPayload(
  value: ProfileFormValue,
  version: string,
  password: string,
): UpdateManagerIdentityPayload {
  const cpf = unmask('cpf', value.manager.cpf);
  return {
    version,
    password,
    fullName: cleanText(value.manager.fullName),
    birthDate: unmask('date', value.manager.birthDate),
    ...(cpf === '' ? {} : { cpf }),
  };
}

/** Mudou algum campo livre? Compara o valor que iria para a API, então espaço e máscara não contam. */
export function freeFieldsChanged(original: ProfileFormValue, current: ProfileFormValue): boolean {
  return !sameJson(toFreePayload(original, ''), toFreePayload(current, ''));
}

/** Mudou algum dado sensível? CPF em branco significa manter o atual, então não é mudança. */
export function identityChanged(original: ProfileFormValue, current: ProfileFormValue): boolean {
  return !sameJson(toIdentityPayload(original, '', ''), toIdentityPayload(current, '', ''));
}

function sameJson(first: object, second: object): boolean {
  return JSON.stringify(first) === JSON.stringify(second);
}

function toFormAddress(address: MyCondominium['manager']['address']): AddressFormValue {
  return {
    postalCode: applyMask('cep', address.postalCode),
    street: address.street,
    number: address.number,
    complement: address.complement ?? '',
    neighborhood: address.neighborhood,
    city: address.city,
    state: address.state,
  };
}

function toDisplayDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-');
  return year && month && day ? `${day}/${month}/${year}` : '';
}
