import { ApiProblem } from '../../core/http/api-problem';
import { closeFailureOf } from './close-failure';

function problemOf(code: string, data?: Record<string, unknown>): ApiProblem {
  return { status: 422, code, detail: null, retryAfterSeconds: null, data };
}

describe('closeFailureOf', () => {
  it('leva as leituras do mes quando falta leitura de alguma unidade', () => {
    const failure = closeFailureOf(problemOf('closing.readings_pending'), '2026-11');

    expect(failure.message).toBe(
      'Falta a leitura de água ou gás de alguma unidade. Informe as leituras do mês antes de fechar',
    );
    expect(failure.link).toEqual({
      label: 'Informar as leituras do mês',
      icon: 'water_drop',
      path: '/financeiro/leituras',
      query: { mes: '2026-11' },
    });
  });

  it('diz o que falta nas unidades e leva para a tela de unidades', () => {
    const failure = closeFailureOf(
      problemOf('condominium.apportionment_not_ready', {
        reason: 'fractions_total',
        fractionTotal: 0.05,
      }),
      '2026-11',
    );

    expect(failure.message).toBe(
      'A soma das frações ideais das unidades ativas é 5% e precisa chegar a 100%. Ajuste as frações na tela de unidades',
    );
    expect(failure.link).toEqual({
      label: 'Abrir as unidades',
      icon: 'apartment',
      path: '/unidades',
    });
  });

  it('nao oferece link para os outros erros', () => {
    const failure = closeFailureOf(problemOf('closing.amounts_pending'), '2026-11');

    expect(failure.message).toContain('Informe o valor de todos os lançamentos variáveis');
    expect(failure.link).toBeUndefined();
  });
});
