import { firstValueFrom } from 'rxjs';
import { UnitsApi } from './units.api';
import { Unit } from './units.models';

/** O máximo que a API entrega por página de unidades. */
const PAGE_SIZE = 200;

/**
 * Todas as unidades ativas, página a página, até juntar o total. Serve às telas que escolhem uma unidade numa lista
 * (cadastro de morador, vínculos e veículos).
 */
export async function fetchActiveUnits(api: UnitsApi): Promise<Unit[]> {
  const units: Unit[] = [];
  for (let page = 1; ; page++) {
    const result = await firstValueFrom(
      api.list({ status: 'active', type: null, block: '', search: '', page, pageSize: PAGE_SIZE }),
    );
    units.push(...result.items);
    if (result.items.length === 0 || units.length >= result.total) {
      return units;
    }
  }
}
