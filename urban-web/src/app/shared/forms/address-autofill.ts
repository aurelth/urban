import { PostalCodeAddress } from '../../core/postal-code/postal-code.models';

/** Os campos do endereço que o CEP determina. Número e complemento são sempre da pessoa. */
export interface AddressPatch {
  readonly street?: string;
  readonly neighborhood?: string;
  readonly city?: string;
  readonly state?: string;
}

/**
 * O que o CEP preenche no formulário: rua, bairro, cidade e UF, trocando o que já estava. O que o CEP não
 * informa (rua e bairro de CEP geral de cidade pequena) fica de fora, para não apagar o que a pessoa digitou.
 */
export function addressPatch(found: PostalCodeAddress): AddressPatch {
  const patch: Record<string, string> = {};
  const fields = {
    street: found.street,
    neighborhood: found.neighborhood,
    city: found.city,
    state: found.state,
  };
  for (const [name, value] of Object.entries(fields)) {
    if (value.trim() !== '') {
      patch[name] = value;
    }
  }
  return patch;
}
