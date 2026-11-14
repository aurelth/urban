/** Tamanho de página padrão da lista de veículos (a API aceita de 1 a 200). */
export const VEHICLES_PAGE_SIZE = 50;

/** Veículo como a API devolve: a placa já vem normalizada (maiúsculas, sem hífen) e a versão volta em toda alteração. */
export interface Vehicle {
  readonly id: string;
  readonly unitId: string;
  readonly unitBlock: string | null;
  readonly unitNumber: string;
  readonly plate: string;
  readonly model: string;
  readonly color: string;
  readonly version: string;
}

/** Resposta de GET /api/vehicles. */
export interface VehiclePage {
  readonly items: readonly Vehicle[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/** O que a lista pede à API. Unidade nula e texto vazio significam "sem filtro". */
export interface VehiclesQuery {
  readonly unitId: string | null;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_VEHICLES_QUERY: VehiclesQuery = {
  unitId: null,
  search: '',
  page: 1,
  pageSize: VEHICLES_PAGE_SIZE,
};

/** Corpo de POST /api/vehicles: a placa só com letras e dígitos, em maiúsculas. */
export interface VehiclePayload {
  readonly unitId: string;
  readonly plate: string;
  readonly model: string;
  readonly color: string;
}

/** Corpo de PUT /api/vehicles/{id}: os mesmos campos do cadastro e a versão lida. */
export interface UpdateVehiclePayload extends VehiclePayload {
  readonly version: string;
}
