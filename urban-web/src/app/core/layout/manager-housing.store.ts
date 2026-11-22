import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { MyAccessApi } from '../finance/my-access.api';
import { MyMonthAccess } from '../finance/my-month.models';

interface HousingState {
  access: MyMonthAccess | null;
}

/**
 * Se o síndico mora no condomínio e, então, tem fatura própria para ver: o menu só mostra "Minha fatura" a quem
 * mora. Quem pergunta é a conta do token, e a pergunta é leve (não lê mês nem cobrança). Falha na consulta não
 * trava nada: sem resposta o síndico só fica sem o item e o menu continua o de sempre.
 */
export const ManagerHousingStore = signalStore(
  { providedIn: 'root' },
  withState<HousingState>({ access: null }),
  withComputed(({ access }) => ({ livesHere: computed(() => access() === 'unit') })),
  withMethods((store) => {
    const api = inject(MyAccessApi);
    /** A pessoa cuja resposta vale: a que chega depois de a sessão trocar de dono é descartada. */
    let current: string | null = null;

    return {
      /** Pergunta uma vez por pessoa; depois de uma falha, a próxima chamada tenta de novo. */
      async load(userId: string): Promise<void> {
        if (current === userId) {
          return;
        }
        current = userId;
        patchState(store, { access: null });
        try {
          const { access } = await firstValueFrom(api.get());
          if (current === userId) {
            patchState(store, { access });
          }
        } catch {
          if (current === userId) {
            current = null;
          }
        }
      },
      /** Esquece tudo: a sessão acabou ou a pessoa já não é síndico com condomínio. */
      reset(): void {
        current = null;
        patchState(store, { access: null });
      },
    };
  }),
);
