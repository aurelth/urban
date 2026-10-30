import { AddressPayload } from '../../core/condominium/condominium.models';
import { unmask } from './masks';

/** O endereço como `getRawValue()` o devolve: os textos como aparecem nos campos, com a máscara do CEP. */
export interface AddressFormValue {
  postalCode: string;
  street: string;
  number: string;
  complement: string;
  neighborhood: string;
  city: string;
  state: string;
}

/** Do formulário para o corpo da API: CEP só em dígitos, textos sem espaços sobrando e complemento nulo quando vazio. */
export function toAddressPayload(address: AddressFormValue): AddressPayload {
  return {
    postalCode: unmask('cep', address.postalCode),
    street: cleanText(address.street),
    number: cleanText(address.number),
    complement: cleanText(address.complement) || null,
    neighborhood: cleanText(address.neighborhood),
    city: cleanText(address.city),
    state: address.state,
  };
}

/** Tira os espaços do começo e do fim e junta os repetidos no meio. */
export function cleanText(text: string): string {
  return text.split(/\s+/).filter(Boolean).join(' ');
}
