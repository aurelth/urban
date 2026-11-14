import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { ResidentsApi } from '../../core/residents/residents.api';
import {
  EndOccupancyPayload,
  OccupancyPayload,
  Resident,
  ResidentPayload,
  UpdateResidentPayload,
} from '../../core/residents/residents.models';
import { fetchActiveUnits } from '../../core/units/active-units';
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
const RESIDENT_NOT_FOUND = 'condominium.resident.not_found';

/** As unidades de quem edita um morador só são buscadas quando a pessoa vai adicionar um vínculo. */
export type UnitsStatus = 'idle' | 'loading' | 'ready' | 'error';

interface ResidentFormState {
  status: ResidentFormStatus;
  /** Na edição, o morador como a API o devolveu por último (com a versão a enviar na próxima gravação). */
  resident: Resident | null;
  /** No cadastro, o morador que acabou de ser criado; a tela troca o formulário pela confirmação. */
  created: Resident | null;
  /** Todas as unidades ativas, para escolher a do vínculo (no cadastro, ou ao adicionar uma unidade na edição). */
  units: readonly Unit[];
  unitsStatus: UnitsStatus;
  /** Um vínculo está sendo gravado (novo ou saída): a tela trava os botões dele sem esconder o formulário. */
  occupancyBusy: boolean;
}

/** Estado do formulário de morador, de cadastro e de edição. Fornecido no componente, então cada visita começa do zero. */
export const ResidentFormStore = signalStore(
  withState<ResidentFormState>({
    status: 'loading',
    resident: null,
    created: null,
    units: [],
    unitsStatus: 'idle',
    occupancyBusy: false,
  }),
  withMethods((store) => {
    const residents = inject(ResidentsApi);
    const unitsApi = inject(UnitsApi);

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

    const keepOccupancies = (fresh: Resident): void => {
      const current = store.resident();
      patchState(store, {
        resident: current === null ? fresh : { ...current, occupancies: fresh.occupancies },
      });
    };

    /**
     * Grava um vínculo. Os vínculos têm vida própria na API: trocam só `occupancies` do morador lido e deixam a versão, a
     * situação e o conflito dos dados pessoais como estavam, senão uma saída registrada "curaria" um formulário velho
     * e o salvamento seguinte passaria por cima da edição de outra pessoa.
     */
    const saveOccupancy = async (request: Observable<Resident>): Promise<ApiProblem | null> => {
      patchState(store, { occupancyBusy: true });
      try {
        keepOccupancies(await firstValueFrom(request));
        return null;
      } catch (error) {
        const problem = toApiProblem(error);
        if (isResidentNotFound(problem)) {
          patchState(store, { status: 'not-found' });
        }
        return problem;
      } finally {
        patchState(store, { occupancyBusy: false });
      }
    };

    return {
      /** Cadastro: busca as unidades ativas. Serve também para tentar de novo depois de um erro. */
      loadForCreate: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          patchState(store, { status: 'ready', units: await fetchActiveUnits(unitsApi) });
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
      /** Edição: busca as unidades ativas na primeira vez que a pessoa vai adicionar um vínculo (ou ao tentar de novo). */
      loadUnits: async (): Promise<void> => {
        if (store.unitsStatus() === 'ready' || store.unitsStatus() === 'loading') {
          return;
        }
        patchState(store, { unitsStatus: 'loading' });
        try {
          patchState(store, { unitsStatus: 'ready', units: await fetchActiveUnits(unitsApi) });
        } catch {
          patchState(store, { unitsStatus: 'error' });
        }
      },
      addOccupancy: (id: string, payload: OccupancyPayload): Promise<ApiProblem | null> =>
        saveOccupancy(residents.addOccupancy(id, payload)),
      endOccupancy: (
        id: string,
        occupancyId: string,
        payload: EndOccupancyPayload,
      ): Promise<ApiProblem | null> =>
        saveOccupancy(residents.endOccupancy(id, occupancyId, payload)),
      /** Depois de uma recusa por tela desatualizada: lê os vínculos de novo, sem mexer no resto. Falha que não é 404 é ignorada. */
      refreshOccupancies: async (id: string): Promise<void> => {
        try {
          keepOccupancies(await firstValueFrom(residents.get(id)));
        } catch (error) {
          if (isResidentNotFound(toApiProblem(error))) {
            patchState(store, { status: 'not-found' });
          }
        }
      },
      /**
       * Lê o morador de novo sem passar por "loading" (o formulário continua na tela). Depois do convite a pessoa ganha a conta,
       * então o acesso e a versão mudam; falha que não é 404 é ignorada (a próxima gravação recusa a versão velha).
       */
      refreshResident: async (id: string): Promise<void> => {
        try {
          patchState(store, { resident: await firstValueFrom(residents.get(id)) });
        } catch (error) {
          if (isResidentNotFound(toApiProblem(error))) {
            patchState(store, { status: 'not-found' });
          }
        }
      },
      /** Volta do aviso de sucesso para o formulário vazio; as unidades já estão carregadas. */
      startAnother: (): void => patchState(store, { created: null }),
    };
  }),
);

/** Só o 409 de versão é "conflito"; o 409 de CPF repetido é um erro de campo e a tela continua editável. */
function statusAfterFailure(problem: ApiProblem): ResidentFormStatus {
  if (isResidentNotFound(problem)) {
    return 'not-found';
  }
  if (problem.status === CONFLICT_STATUS && problem.code === CONCURRENCY_CONFLICT) {
    return 'conflict';
  }
  return 'ready';
}

function isResidentNotFound(problem: ApiProblem): boolean {
  return problem.status === NOT_FOUND_STATUS && problem.code === RESIDENT_NOT_FOUND;
}
