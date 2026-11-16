import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom, map } from 'rxjs';
import { FinanceApi } from '../../core/finance/finance.api';
import { MonthEntries } from '../../core/finance/finance.models';
import { PaymentInstructionsApi } from '../../core/finance/payment-instructions.api';
import { PaymentInstructions } from '../../core/finance/payment-instructions.models';
import { PaymentsApi } from '../../core/finance/payments.api';
import { MonthPayments } from '../../core/finance/payments.models';
import { ResidentsApi } from '../../core/residents/residents.api';
import { UnitsApi } from '../../core/units/units.api';
import { UnitsSummary } from '../../core/units/units.models';
import { PendingItem, buildPendings } from './dashboard-values';

export type SectionStatus = 'loading' | 'ready' | 'error';

/** Uma leitura do painel: cada cartão carrega e falha sozinho. */
export interface Section<T> {
  readonly status: SectionStatus;
  readonly value: T | null;
}

/** O que o cartão de recebimentos mostra: nada enquanto o mês está aberto, e os totais depois do fechamento. */
export type PaymentsOverview =
  | { readonly closed: false }
  | {
      readonly closed: true;
      readonly charged: number;
      readonly received: number;
      /** Quantas cobranças do mês estão em atraso. */
      readonly overdue: number;
    };

interface DashboardState {
  month: Section<MonthEntries>;
  payments: Section<PaymentsOverview>;
  units: Section<UnitsSummary>;
  residents: Section<number>;
  instructions: Section<PaymentInstructions>;
  monthKey: string;
}

const LOADING = { status: 'loading', value: null } as const;
const FAILED = { status: 'error', value: null } as const;
const ONE_ITEM = { page: 1, pageSize: 1 } as const;

async function settle<T>(request: Observable<T>): Promise<Section<T>> {
  try {
    return { status: 'ready', value: await firstValueFrom(request) };
  } catch {
    return FAILED;
  }
}

function overview(data: MonthPayments): PaymentsOverview {
  return { closed: true, charged: data.charged, received: data.received, overdue: data.total };
}

function worst(...statuses: SectionStatus[]): SectionStatus {
  if (statuses.includes('error')) {
    return 'error';
  }
  return statuses.includes('loading') ? 'loading' : 'ready';
}

/**
 * Estado do painel do síndico. Fornecido no componente, então cada visita começa do zero. Só lê (nada é gravado) e usa as
 * APIs que as telas já usam: o mês, as cobranças em atraso, as unidades, os moradores atuais e os dados de pagamento.
 */
export const DashboardStore = signalStore(
  withState<DashboardState>({
    month: LOADING,
    payments: LOADING,
    units: LOADING,
    residents: LOADING,
    instructions: LOADING,
    monthKey: '',
  }),
  withComputed((store) => ({
    condominiumStatus: computed(() => worst(store.units().status, store.residents().status)),
    pendingsStatus: computed(() =>
      worst(
        store.month().status,
        store.payments().status,
        store.units().status,
        store.instructions().status,
      ),
    ),
    pendings: computed<PendingItem[]>(() => {
      const payments = store.payments().value;
      return buildPendings({
        month: store.month().value,
        overdueCharges: payments?.closed ? payments.overdue : null,
        units: store.units().value,
        instructions: store.instructions().value,
      });
    }),
  })),
  withMethods((store) => {
    const finance = inject(FinanceApi);
    const paymentsApi = inject(PaymentsApi);
    const unitsApi = inject(UnitsApi);
    const residentsApi = inject(ResidentsApi);
    const instructionsApi = inject(PaymentInstructionsApi);

    /** O mês e, se ele já foi fechado, as cobranças em atraso (uma só página basta: o que importa é o total). */
    const loadMonth = async (): Promise<void> => {
      const key = store.monthKey();
      patchState(store, { month: LOADING, payments: LOADING });
      const month = await settle(finance.getMonth(key));
      patchState(store, { month });
      if (month.value === null) {
        patchState(store, { payments: FAILED });
      } else if (month.value.closing === null) {
        patchState(store, { payments: { status: 'ready', value: { closed: false } } });
      } else {
        const request = paymentsApi.listMonth(key, { status: 'overdue', ...ONE_ITEM });
        patchState(store, { payments: await settle(request.pipe(map(overview))) });
      }
    };

    const loadUnits = async (): Promise<void> => {
      patchState(store, { units: LOADING });
      patchState(store, { units: await settle(unitsApi.summary()) });
    };

    const loadResidents = async (): Promise<void> => {
      patchState(store, { residents: LOADING });
      const request = residentsApi.list({
        status: 'active',
        registration: 'all',
        search: '',
        ...ONE_ITEM,
      });
      patchState(store, { residents: await settle(request.pipe(map((page) => page.total))) });
    };

    const loadInstructions = async (): Promise<void> => {
      patchState(store, { instructions: LOADING });
      patchState(store, { instructions: await settle(instructionsApi.get()) });
    };

    const loadCondominium = async (): Promise<void> => {
      await Promise.all([loadUnits(), loadResidents()]);
    };

    return {
      async load(month: string): Promise<void> {
        patchState(store, { monthKey: month });
        await Promise.all([loadMonth(), loadCondominium(), loadInstructions()]);
      },
      retryMonth: loadMonth,
      retryCondominium: loadCondominium,
      /** Refaz só o que falhou, para as pendências deixarem de estar incompletas. */
      async retryFailed(): Promise<void> {
        const retries: Promise<void>[] = [];
        if (store.month().status === 'error' || store.payments().status === 'error') {
          retries.push(loadMonth());
        }
        if (store.units().status === 'error') {
          retries.push(loadUnits());
        }
        if (store.residents().status === 'error') {
          retries.push(loadResidents());
        }
        if (store.instructions().status === 'error') {
          retries.push(loadInstructions());
        }
        await Promise.all(retries);
      },
    };
  }),
);
