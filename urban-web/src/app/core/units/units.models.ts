export type UnitKind = 'apartment' | 'house';

export type UnitStatusFilter = 'active' | 'deactivated' | 'all';

export type ApportionmentMethod = 'idealFraction' | 'equalShare' | 'area';

/** Unidade como a API devolve: a fração ideal vai de 0 a 1 (0,0125 é 1,25%) e a versão volta em toda alteração. */
export interface Unit {
  readonly id: string;
  readonly block: string | null;
  readonly number: string;
  readonly type: UnitKind;
  readonly idealFraction: number | null;
  readonly area: number | null;
  readonly isActive: boolean;
  readonly deactivatedAt: string | null;
  readonly version: string;
}

/** Resposta de GET /api/units. */
export interface UnitPage {
  readonly items: readonly Unit[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface MethodReadiness {
  readonly method: ApportionmentMethod;
  readonly ready: boolean;
}

/** Resposta de GET /api/units/summary: contagens, fração ocupada e a prontidão de cada método de rateio. */
export interface UnitsSummary {
  readonly active: number;
  readonly deactivated: number;
  readonly houses: number;
  readonly apartments: number;
  readonly idealFractionTotal: number;
  readonly withoutIdealFraction: number;
  readonly withoutArea: number;
  readonly apportionmentMethod: ApportionmentMethod;
  readonly apportionmentVersion: string;
  readonly readiness: readonly MethodReadiness[];
}

/** Corpo de POST /api/units e de cada item do lote. Bloco nulo quando não há; fração entre 0 e 1. */
export interface UnitPayload {
  readonly block: string | null;
  readonly number: string;
  readonly type: UnitKind;
  readonly idealFraction: number | null;
  readonly area: number | null;
}

/** Corpo de PUT /api/units/{id}: os mesmos campos, mais a versão lida. */
export interface UpdateUnitPayload extends UnitPayload {
  readonly version: string;
}

export interface BatchCreated {
  readonly created: number;
}

export interface ApportionmentChanged {
  readonly method: ApportionmentMethod;
  readonly version: string;
}

/** O que a lista pede à API. Texto vazio e tipo nulo significam "sem filtro". */
export interface UnitsQuery {
  readonly status: UnitStatusFilter;
  readonly type: UnitKind | null;
  readonly block: string;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

export const UNITS_PAGE_SIZE = 50;

export const DEFAULT_UNITS_QUERY: UnitsQuery = {
  status: 'active',
  type: null,
  block: '',
  search: '',
  page: 1,
  pageSize: UNITS_PAGE_SIZE,
};
