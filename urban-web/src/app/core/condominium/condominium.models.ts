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

/** Endereço como a API devolve: igual ao do envio, sem máscara. */
export type AddressView = AddressPayload;

/** Resposta de GET /api/condominiums/mine: o cadastro do síndico com o CPF mascarado e a versão para a edição. */
export interface MyCondominium {
  readonly condominiumId: string;
  /** Devolvida na edição; se outra pessoa mudou os dados no meio do caminho, a API recusa com 409. */
  readonly version: string;
  readonly displayName: string | null;
  readonly manager: {
    readonly fullName: string;
    readonly maskedCpf: string;
    readonly birthDate: string;
    readonly phone: string;
    readonly address: AddressView;
  };
  readonly condominium: {
    readonly name: string;
    readonly cnpj: string;
    readonly address: AddressView;
  };
}

/** Corpo de PUT /api/condominiums/mine: só os campos livres. O CNPJ nunca muda. */
export interface UpdateMyCondominiumPayload {
  readonly version: string;
  readonly displayName: string;
  readonly phone: string;
  readonly managerAddress: AddressPayload;
  readonly condominiumName: string;
  readonly condominiumAddress: AddressPayload;
}

/** Corpo de PUT /api/condominiums/mine/manager-identity: dados sensíveis, com a senha. Sem CPF, mantém o atual. */
export interface UpdateManagerIdentityPayload {
  readonly version: string;
  readonly password: string;
  readonly fullName: string;
  readonly birthDate: string;
  readonly cpf?: string;
}
