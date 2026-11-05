import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { BatchIssue, toBatchIssues } from '../../core/units/batch-issues';
import { UnitsApi } from '../../core/units/units.api';
import { UnitPayload } from '../../core/units/units.models';

interface BatchState {
  saving: boolean;
  /** Problemas por linha da última recusa; a posição é a da pré-visualização na hora do envio. */
  issues: readonly BatchIssue[];
  /** Quantas unidades o último envio cadastrou; nulo enquanto o lote não foi gravado. */
  created: number | null;
}

/** Estado do cadastro em lote. Fornecido no componente, então cada visita começa do zero. */
export const UnitsBatchStore = signalStore(
  withState<BatchState>({ saving: false, issues: [], created: null }),
  withMethods((store) => {
    const api = inject(UnitsApi);

    return {
      /** Grava o lote inteiro. Devolve o problema (ou nulo, se deu certo) e guarda os problemas de cada linha. */
      submit: async (units: readonly UnitPayload[]): Promise<ApiProblem | null> => {
        patchState(store, { saving: true, issues: [] });
        try {
          const result = await firstValueFrom(api.createBatch(units));
          patchState(store, { saving: false, created: result.created });
          return null;
        } catch (error) {
          patchState(store, { saving: false, issues: toBatchIssues(error) });
          return toApiProblem(error);
        }
      },
      clearIssue: (position: number): void => {
        patchState(store, {
          issues: store.issues().filter((issue) => issue.position !== position),
        });
      },
      clearIssues: (): void => {
        patchState(store, { issues: [] });
      },
      startOver: (): void => {
        patchState(store, { created: null, issues: [] });
      },
    };
  }),
);
