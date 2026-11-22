import { inject } from '@angular/core';
import { MyMonthApi } from '../../core/finance/my-month.api';
import { createMonthStore } from '../finance/month-store';

/** Estado da tela "Minha fatura". Fornecido no componente, então cada visita começa do zero. */
export const MyMonthStore = createMonthStore(() => {
  const api = inject(MyMonthApi);
  return (month) => api.get(month);
});
