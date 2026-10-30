import { DraftAddress, RegistrationDraftData } from '../../../core/drafts/registration-draft';
import { AddressFormValue } from '../../../shared/forms/address.payload';
import { RegisterCondominiumFormValue } from './register-condominium.payload';

/**
 * Do formulário para o rascunho, por LISTA DE CAMPOS PERMITIDOS: o que não está aqui não é guardado, nem se a tela
 * ganhar um campo novo amanhã. CPF, data de nascimento e senha ficam de fora de propósito, porque o rascunho mora no
 * navegador, um lugar que outra pessoa com acesso ao computador pode abrir.
 */
export function toDraftData(
  value: RegisterCondominiumFormValue,
  sameAddress: boolean,
): RegistrationDraftData {
  const { manager, condominium } = value;
  return {
    manager: {
      fullName: manager.fullName,
      displayName: manager.displayName,
      phone: manager.phone,
      address: toDraftAddress(manager.address),
    },
    condominium: {
      name: condominium.name,
      cnpj: condominium.cnpj,
      address: toDraftAddress(condominium.address),
    },
    sameAddress,
  };
}

function toDraftAddress(address: AddressFormValue): DraftAddress {
  return {
    postalCode: address.postalCode,
    street: address.street,
    number: address.number,
    complement: address.complement,
    neighborhood: address.neighborhood,
    city: address.city,
    state: address.state,
  };
}
