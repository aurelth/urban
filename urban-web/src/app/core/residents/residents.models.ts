export type OccupancyKind = 'owner' | 'tenant';

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

export const RESIDENTS_PAGE_SIZE = 50;

/** A tela abre com quem mora hoje; quem já saiu fica a um filtro de distância. */
export const DEFAULT_RESIDENTS_QUERY: ResidentsQuery = {
  status: 'active',
  search: '',
  page: 1,
  pageSize: RESIDENTS_PAGE_SIZE,
};
