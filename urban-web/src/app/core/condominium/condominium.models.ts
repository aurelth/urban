/** Endereço como a API recebe: CEP só em dígitos e complemento nulo quando não há. */
export interface AddressPayload {
  readonly postalCode: string;
  readonly street: string;
  readonly number: string;
  readonly complement: string | null;
  readonly neighborhood: string;
  readonly city: string;
  readonly state: string;
}

/** Corpo de POST /api/condominiums. CPF, CNPJ, CEP e telefone só em dígitos; a data em aaaa-mm-dd. */
export interface RegisterCondominiumPayload {
  readonly displayName: string;
  readonly manager: {
    readonly fullName: string;
    readonly cpf: string;
    readonly birthDate: string;
    readonly phone: string;
    readonly address: AddressPayload;
  };
  readonly condominium: {
    readonly name: string;
    readonly cnpj: string;
    readonly address: AddressPayload;
  };
}

/** Resposta de POST /api/condominiums: só o que a tela precisa, com o CPF já mascarado. */
export interface RegisteredCondominium {
  readonly condominiumId: string;
  readonly name: string;
  readonly cnpj: string;
  readonly managerCpf: string;
}
