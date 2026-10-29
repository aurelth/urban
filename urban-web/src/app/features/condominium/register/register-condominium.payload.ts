import {
  AddressPayload,
  RegisterCondominiumPayload,
} from '../../../core/condominium/condominium.models';
import { unmask } from '../../../shared/forms/masks';

export interface AddressFormValue {
  postalCode: string;
  street: string;
  number: string;
  complement: string;
  neighborhood: string;
  city: string;
  state: string;
}

/** O que `form.getRawValue()` devolve na tela de cadastro, com os textos como aparecem nos campos. */
export interface RegisterCondominiumFormValue {
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
    cnpj: string;
    address: AddressFormValue;
  };
}

/**
 * Do formulário para o corpo da API: sem pontuação nos documentos, CEP e telefone, data em
 * aaaa-mm-dd, textos sem espaços sobrando e complemento nulo quando vazio. Com `sameAddress`, o
 * condomínio usa o endereço do síndico e o que estiver nos campos dele é ignorado.
 */
export function toRegisterPayload(
  value: RegisterCondominiumFormValue,
  sameAddress: boolean,
): RegisterCondominiumPayload {
  const { manager, condominium } = value;
  const managerAddress = toAddress(manager.address);

  return {
    displayName: clean(manager.displayName),
    manager: {
      fullName: clean(manager.fullName),
      cpf: unmask('cpf', manager.cpf),
      birthDate: unmask('date', manager.birthDate),
      phone: unmask('phone', manager.phone),
      address: managerAddress,
    },
    condominium: {
      name: clean(condominium.name),
      cnpj: unmask('cnpj', condominium.cnpj),
      address: sameAddress ? managerAddress : toAddress(condominium.address),
    },
  };
}

function toAddress(address: AddressFormValue): AddressPayload {
  return {
    postalCode: unmask('cep', address.postalCode),
    street: clean(address.street),
    number: clean(address.number),
    complement: clean(address.complement) || null,
    neighborhood: clean(address.neighborhood),
    city: clean(address.city),
    state: address.state,
  };
}

function clean(text: string): string {
  return text.split(/\s+/).filter(Boolean).join(' ');
}
