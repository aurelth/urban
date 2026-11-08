import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom, of, switchMap } from 'rxjs';
import { FinanceApi } from '../../core/finance/finance.api';
import {
  Expense,
  ExpenseGroup,
  ExpenseGroupValues,
  ExpensePayload,
  ExpenseStatusFilter,
  UpdateExpensePayload,
} from '../../core/finance/finance.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';

export type FinanceStatus = 'loading' | 'ready' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';
const NOT_FOUND_STATUS = 404;

type ExpenseChanges = Omit<UpdateExpensePayload, 'version'>;

interface ExpensesState {
  status: FinanceStatus;
  filter: ExpenseStatusFilter;
  groups: readonly ExpenseGroup[];
  expenses: readonly Expense[];
  busy: boolean;
}

/** Estado da tela de despesas. Fornecido no componente, então cada visita começa do zero. */
export const ExpensesStore = signalStore(
  withState<ExpensesState>({
    status: 'loading',
    filter: 'active',
    groups: [],
    expenses: [],
    busy: false,
  }),
  withMethods((store) => {
    const api = inject(FinanceApi);

    const fetchAll = async (filter: ExpenseStatusFilter): Promise<void> => {
      const [groups, expenses] = await Promise.all([
        firstValueFrom(api.listGroups()),
        firstValueFrom(api.listExpenses(filter)),
      ]);
      patchState(store, { groups, expenses });
    };

    const refresh = async (): Promise<void> => {
      try {
        await fetchAll(store.filter());
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    /** Faz uma gravação e atualiza a tela. Devolve o problema (ou nulo) para a tela explicar. */
    const act = async (request: Observable<unknown>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || isStale(problem)) {
        await refresh();
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Garante o grupo padrão e busca grupos e despesas. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          await firstValueFrom(api.ensureDefaultGroup());
          await fetchAll(store.filter());
          patchState(store, { status: 'ready' });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      filterBy: async (filter: ExpenseStatusFilter): Promise<void> => {
        patchState(store, { busy: true });
        try {
          patchState(store, { expenses: await firstValueFrom(api.listExpenses(filter)), filter });
        } catch {
          patchState(store, { status: 'error' });
        }
        patchState(store, { busy: false });
      },
      groupName: (id: string): string =>
        store.groups().find((group) => group.id === id)?.name ?? '—',
      createExpense: (payload: ExpensePayload): Promise<ApiProblem | null> =>
        act(api.createExpense(payload)),
      updateExpense: (target: Expense, changes: ExpenseChanges): Promise<ApiProblem | null> =>
        act(api.updateExpense(target.id, { ...changes, version: target.version })),
      changeAmount: (
        target: Expense,
        fromMonth: string,
        amount: number,
      ): Promise<ApiProblem | null> =>
        act(api.changeAmount(target.id, target.version, fromMonth, amount)),
      setInstallment: (
        target: Expense,
        number: number,
        amount: number,
      ): Promise<ApiProblem | null> =>
        act(api.setInstallment(target.id, target.version, number, amount)),
      end: (target: Expense, lastMonth: string): Promise<ApiProblem | null> =>
        act(api.endExpense(target.id, target.version, lastMonth)),
      /** Cria o grupo e, se ele entra na base do fundo de reserva, marca com a versão que a API acabou de devolver. */
      createGroup: (values: ExpenseGroupValues): Promise<ApiProblem | null> =>
        act(
          api
            .createGroup(values.name)
            .pipe(
              switchMap((created) =>
                values.reserveFundBase
                  ? api.setGroupReserveFundBase(created.id, created.version, true)
                  : of(created),
              ),
            ),
        ),
      /** Renomeia e/ou muda a marca, uma chamada de cada vez: a segunda usa a versão devolvida pela primeira. */
      updateGroup: (
        target: ExpenseGroup,
        values: ExpenseGroupValues,
      ): Promise<ApiProblem | null> => {
        const renamed: Observable<ExpenseGroup> =
          values.name === target.name
            ? of(target)
            : api.renameGroup(target.id, target.version, values.name);
        return act(
          renamed.pipe(
            switchMap((group) =>
              values.reserveFundBase === target.reserveFundBase
                ? of(group)
                : api.setGroupReserveFundBase(group.id, group.version, values.reserveFundBase),
            ),
          ),
        );
      },
    };
  }),
);

function isStale(problem: ApiProblem): boolean {
  return problem.status === NOT_FOUND_STATUS || problem.code === CONCURRENCY_CONFLICT;
}
