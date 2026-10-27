import { ApiProblem } from './api-problem';

/** Texto da tela para cada código da API. A interface é dona do texto; a API só garante o código. */
export type MessagesByCode = Readonly<Record<string, string>>;

export function describeProblem(problem: ApiProblem, byCode: MessagesByCode): string {
  if (problem.code !== null && Object.hasOwn(byCode, problem.code)) {
    return byCode[problem.code];
  }
  switch (problem.status) {
    case 0:
      return 'Não foi possível conectar ao Urban. Verifique sua conexão e tente de novo';
    case 429:
      return `Muitas tentativas em pouco tempo. Aguarde ${waitTime(problem.retryAfterSeconds)} e tente de novo`;
    case 503:
      return 'Serviço temporariamente indisponível. Tente novamente em instantes';
    default:
      return 'Algo deu errado. Tente novamente em instantes';
  }
}

function waitTime(seconds: number | null): string {
  if (seconds === null) {
    return 'alguns instantes';
  }
  return `${seconds} ${seconds === 1 ? 'segundo' : 'segundos'}`;
}
