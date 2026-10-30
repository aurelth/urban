import { RegisterCondominiumPayload } from '../../../core/condominium/condominium.models';
import {
  AddressFormValue,
  cleanText,
  toAddressPayload,
} from '../../../shared/forms/address.payload';
import { unmask } from '../../../shared/forms/masks';

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
  const managerAddress = toAddressPayload(manager.address);

  return {
    displayName: cleanText(manager.displayName),
    manager: {
      fullName: cleanText(manager.fullName),
      cpf: unmask('cpf', manager.cpf),
      birthDate: unmask('date', manager.birthDate),
      phone: unmask('phone', manager.phone),
      address: managerAddress,
    },
    condominium: {
      name: cleanText(condominium.name),
      cnpj: unmask('cnpj', condominium.cnpj),
      address: sameAddress ? managerAddress : toAddressPayload(condominium.address),
    },
  };
}
