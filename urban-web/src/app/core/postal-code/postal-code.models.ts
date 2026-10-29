/** Endereço de um CEP, como a API devolve. Rua e bairro vêm vazios nos CEPs gerais de cidades pequenas. */
export interface PostalCodeAddress {
  readonly postalCode: string;
  readonly street: string;
  readonly neighborhood: string;
  readonly city: string;
  readonly state: string;
}

/** O que uma consulta de CEP pode resultar: a tela só precisa distinguir estes três casos. */
export type PostalCodeLookup =
  | { readonly status: 'found'; readonly address: PostalCodeAddress }
  | { readonly status: 'not-found' }
  | { readonly status: 'unavailable' };
