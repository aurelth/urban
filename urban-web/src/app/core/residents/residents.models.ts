export type OccupancyKind = 'owner' | 'tenant' | 'dependent';

/**
 * Situação do acesso do morador ao sistema: sem convite, convite enviado (a senha ainda não foi escolhida: o e-mail ainda se
 * corrige), acesso ativo ou desativado (conta desativada, suspensa ou excluída).
 */
export type ResidentAccessStatus = 'none' | 'invited' | 'active' | 'inactive';

export type ResidentStatusFilter = 'active' | 'former' | 'all';

export interface EmergencyContact {
  readonly name: string;
  readonly relationship: string;
  readonly phone: string;
}

/** Vínculo do morador com uma unidade. Datas em AAAA-MM-DD; a saída é nula enquanto a pessoa mora na unidade. */
export interface ResidentOccupancy {
  readonly id: string;
  readonly unitId: string;
  readonly unitBlock: string | null;
  readonly unitNumber: string;
  readonly kind: OccupancyKind;
  readonly startDate: string;
  readonly endDate: string | null;
}

/** Morador como a API devolve: o CPF só vem mascarado e a versão volta em toda alteração. */
export interface Resident {
  readonly id: string;
  readonly fullName: string;
  readonly maskedCpf: string;
  readonly birthDate: string;
  readonly phone: string;
  readonly email: string | null;
  readonly profession: string | null;
  readonly emergencyContact: EmergencyContact | null;
  readonly accessStatus: ResidentAccessStatus;
  readonly occupancies: readonly ResidentOccupancy[];
  readonly version: string;
}

/** Resposta de GET /api/residents. */
export interface ResidentPage {
  readonly items: readonly Resident[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/** O que a lista pede à API. Texto vazio significa "sem busca". */
export interface ResidentsQuery {
  readonly status: ResidentStatusFilter;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

/**
 * Corpo de POST /api/residents: só dígitos nos documentos e telefones, datas em AAAA-MM-DD. O e-mail é opcional (nulo
 * quando não há) e o primeiro vínculo, com a unidade, vai junto. Profissão e contato de emergência só podem ser nulos
 * para menores de 18 anos, que são sempre dependentes.
 */
export interface ResidentPayload {
  readonly fullName: string;
  readonly cpf: string;
  readonly birthDate: string;
  readonly phone: string;
  readonly email: string | null;
  readonly profession: string | null;
  readonly emergencyContact: EmergencyContact | null;
  readonly unitId: string;
  readonly kind: OccupancyKind;
  readonly startDate: string;
}

/**
 * Corpo de PUT /api/residents/{id}: os dados pessoais do cadastro e a versão lida. Sem a chave `cpf` a API mantém o
 * atual (a resposta só mostra o CPF mascarado); e-mail nulo limpa o e-mail.
 */
export interface UpdateResidentPayload {
  readonly version: string;
  readonly fullName: string;
  readonly cpf?: string;
  readonly birthDate: string;
  readonly phone: string;
  readonly email: string | null;
  readonly profession: string | null;
  readonly emergencyContact: EmergencyContact | null;
}

/** Corpo de POST /api/residents/{id}/occupancies: a unidade, o tipo do vínculo e o dia da entrada (AAAA-MM-DD). */
export interface OccupancyPayload {
  readonly unitId: string;
  readonly kind: OccupancyKind;
  readonly startDate: string;
}

/** Corpo de POST /api/residents/{id}/occupancies/{occupancyId}/end: o dia da saída (AAAA-MM-DD). */
export interface EndOccupancyPayload {
  readonly endDate: string;
}

export const RESIDENTS_PAGE_SIZE = 50;

/** A tela abre com quem mora hoje; quem já saiu fica a um filtro de distância. */
export const DEFAULT_RESIDENTS_QUERY: ResidentsQuery = {
  status: 'active',
  search: '',
  page: 1,
  pageSize: RESIDENTS_PAGE_SIZE,
};
