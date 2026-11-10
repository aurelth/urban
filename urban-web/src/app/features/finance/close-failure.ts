import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { CloseFailure } from './close-month.dialog';
import { FINANCE_FORM_MESSAGES } from './finance.messages';

/**
 * Por que o fechamento do mês foi recusado, em texto da tela, e para onde a pessoa vai resolver: faltou leitura de água ou gás
 * (leva às leituras do mês) ou os dados das unidades não bastam para o rateio (leva às unidades). Os demais erros só têm o texto.
 */
export function closeFailureOf(problem: ApiProblem, month: string): CloseFailure {
  const message = describeProblem(problem, FINANCE_FORM_MESSAGES);
  switch (problem.code) {
    case 'closing.readings_pending':
      return {
        message,
        link: {
          label: 'Informar as leituras do mês',
          icon: 'water_drop',
          path: '/financeiro/leituras',
          query: { mes: month },
        },
      };
    case 'condominium.apportionment_not_ready':
      return {
        message,
        link: { label: 'Abrir as unidades', icon: 'apartment', path: '/unidades' },
      };
    default:
      return { message };
  }
}
