import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { ResidentsApi } from '../../core/residents/residents.api';
import {
  Resident,
  ResidentPayload,
  UpdateResidentPayload,
} from '../../core/residents/residents.models';
import { UnitsApi } from '../../core/units/units.api';
import { Unit } from '../../core/units/units.models';

/**
 * - loading: buscando (o morador, na edição; as unidades, no cadastro);
 * - ready: formulário na tela, pronto para editar;
 * - saving: gravando (a tela trava o botão);
 * - conflict: outra pessoa mudou o morador depois da leitura (409 de versão); a tela pede para recarregar;
 * - not-found: o morador não existe (404);
 * - error: a busca falhou por outro motivo.
 */
export type ResidentFormStatus =
  'loading' | 'ready' | 'saving' | 'conflict' | 'not-found' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';
const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;
/** O maior tamanho de página que a API de unidades aceita. */
const UNITS_PAGE_SIZE = 200;

interface ResidentFormState {
  status: ResidentFormStatus;
  /** Na edição, o morador como a API o devolveu por último (com a versão a enviar na próxima gravação). */
  resident: Resident | null;
  /** No cadastro, o morador que acabou de ser criado; a tela troca o formulário pela confirmação. */
  created: Resident | null;
  /** No cadastro, todas as unidades ativas, para escolher a do vínculo. */
  units: readonly Unit[];
}

/** Estado do formulário de morador, de cadastro e de edição. Fornecido no componente, então cada visita começa do zero. */
export const ResidentFormStore = signalStore(
  withState<ResidentFormState>({ status: 'loading', resident: null, created: null, units: [] }),
  withMethods((store) => {
    const residents = inject(ResidentsApi);
    const unitsApi = inject(UnitsApi);

    /** As unidades ativas, página a página (a API entrega até 200 por vez), até juntar todas. */
    const fetchActiveUnits = async (): Promise<Unit[]> => {
      const units: Unit[] = [];
      for (let page = 1; ; page++) {
        const result = await firstValueFrom(
          unitsApi.list({
            status: 'active',
            type: null,
            block: '',
            search: '',
            page,
            pageSize: UNITS_PAGE_SIZE,
          }),
        );
        units.push(...result.items);
        if (result.items.length === 0 || units.length >= result.total) {
          return units;
        }
      }
    };

    /** Grava, guarda o que a API devolver com `keep` e devolve o problema (ou nulo) para a tela explicar. */
    const save = async (
      request: Observable<Resident>,
      keep: (saved: Resident) => Partial<ResidentFormState>,
    ): Promise<ApiProblem | null> => {
      patchState(store, { status: 'saving' });
      try {
        const saved = await firstValueFrom(request);
        patchState(store, { status: 'ready', ...keep(saved) });
        return null;
      } catch (error) {
        const problem = toApiProblem(error);
        patchState(store, { status: statusAfterFailure(problem) });
        return problem;
      }
    };

    return {
      /** Cadastro: busca as unidades ativas. Serve também para tentar de novo depois de um erro. */
      loadForCreate: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          patchState(store, { status: 'ready', units: await fetchActiveUnits() });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      /** Edição: busca o morador. Serve à primeira carga, a tentar de novo e a recarregar depois de um conflito. */
      loadForEdit: async (id: string): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          patchState(store, { status: 'ready', resident: await firstValueFrom(residents.get(id)) });
        } catch (error) {
          const notFound = toApiProblem(error).status === NOT_FOUND_STATUS;
          patchState(store, { status: notFound ? 'not-found' : 'error', resident: null });
        }
      },
      create: (payload: ResidentPayload): Promise<ApiProblem | null> =>
        save(residents.create(payload), (created) => ({ created })),
      update: (id: string, payload: UpdateResidentPayload): Promise<ApiProblem | null> =>
        save(residents.update(id, payload), (resident) => ({ resident })),
      /** Volta do aviso de sucesso para o formulário vazio; as unidades já estão carregadas. */
      startAnother: (): void => patchState(store, { created: null }),
    };
  }),
);

/** Só o 409 de versão é "conflito"; o 409 de CPF repetido é um erro de campo e a tela continua editável. */
function statusAfterFailure(problem: ApiProblem): ResidentFormStatus {
  if (problem.status === NOT_FOUND_STATUS && problem.code === 'condominium.resident.not_found') {
    return 'not-found';
  }
  if (problem.status === CONFLICT_STATUS && problem.code === CONCURRENCY_CONFLICT) {
    return 'conflict';
  }
  return 'ready';
}
