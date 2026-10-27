import { ApiProblem } from './api-problem';
import { describeProblem } from './problem-message';

const BY_CODE = { 'identity.login.invalid_credentials': 'E-mail ou senha incorretos' };

function problem(overrides: Partial<ApiProblem> = {}): ApiProblem {
  return { status: 400, code: null, detail: null, retryAfterSeconds: null, ...overrides };
}

describe('describeProblem', () => {
  it('usa o texto do codigo quando existe', () => {
    const message = describeProblem(
      problem({ status: 401, code: 'identity.login.invalid_credentials' }),
      BY_CODE,
    );

    expect(message).toBe('E-mail ou senha incorretos');
  });

  it('prefere o texto do codigo ao do status', () => {
    const message = describeProblem(
      problem({ status: 429, code: 'identity.login.invalid_credentials' }),
      BY_CODE,
    );

    expect(message).toBe('E-mail ou senha incorretos');
  });

  it('pede para aguardar o tempo informado no 429', () => {
    expect(describeProblem(problem({ status: 429, retryAfterSeconds: 30 }), BY_CODE)).toBe(
      'Muitas tentativas em pouco tempo. Aguarde 30 segundos e tente de novo',
    );
  });

  it('usa o singular quando falta 1 segundo', () => {
    expect(describeProblem(problem({ status: 429, retryAfterSeconds: 1 }), BY_CODE)).toBe(
      'Muitas tentativas em pouco tempo. Aguarde 1 segundo e tente de novo',
    );
  });

  it('pede para aguardar sem prometer um tempo quando a API nao informa', () => {
    expect(describeProblem(problem({ status: 429 }), BY_CODE)).toBe(
      'Muitas tentativas em pouco tempo. Aguarde alguns instantes e tente de novo',
    );
  });

  it('avisa quando nao ha resposta da API', () => {
    expect(describeProblem(problem({ status: 0 }), BY_CODE)).toBe(
      'Não foi possível conectar ao Urban. Verifique sua conexão e tente de novo',
    );
  });

  it('avisa quando o servico esta indisponivel', () => {
    expect(describeProblem(problem({ status: 503 }), BY_CODE)).toBe(
      'Serviço temporariamente indisponível. Tente novamente em instantes',
    );
  });

  it('usa uma mensagem generica para o resto', () => {
    expect(describeProblem(problem({ status: 500 }), BY_CODE)).toBe(
      'Algo deu errado. Tente novamente em instantes',
    );
  });

  it.each(['identity.desconhecido', 'constructor', '__proto__', 'toString'])(
    'ignora o codigo "%s" que nao tem texto',
    (code) => {
      expect(describeProblem(problem({ status: 400, code }), BY_CODE)).toBe(
        'Algo deu errado. Tente novamente em instantes',
      );
    },
  );
});
