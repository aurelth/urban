import { ApiProblem } from '../../core/http/api-problem';
import { describeApportionmentShortfall } from './apportionment-shortfall';

const GENERIC =
  'Complete os dados das unidades, na tela de unidades, para o método de rateio escolhido';

function shortfall(data?: Record<string, unknown>): ApiProblem {
  return {
    status: 422,
    code: 'condominium.apportionment_not_ready',
    detail: null,
    retryAfterSeconds: null,
    data,
  };
}

const unit = (
  number: string,
  block: string | null = null,
): { block: string | null; number: string } => ({
  block,
  number,
});

describe('describeApportionmentShortfall', () => {
  it('diz a soma atual das fracoes e quanto precisa chegar', () => {
    const data = { reason: 'fractions_total', fractionTotal: 0.05 };

    expect(describeApportionmentShortfall(shortfall(data))).toBe(
      'A soma das frações ideais das unidades ativas é 5% e precisa chegar a 100%. Ajuste as frações na tela de unidades',
    );
  });

  it('mostra a soma com casas decimais quando ela nao e redonda', () => {
    const data = { reason: 'fractions_total', fractionTotal: 0.0125 };

    expect(describeApportionmentShortfall(shortfall(data))).toContain('é 1,25% e precisa');
  });

  it('diz que nao ha unidades ativas', () => {
    expect(describeApportionmentShortfall(shortfall({ reason: 'no_active_units' }))).toBe(
      'Não há unidades ativas para dividir o mês. Cadastre as unidades na tela de unidades',
    );
  });

  it.each([
    [
      'fractions_missing',
      [unit('301')],
      1,
      'Falta a fração ideal da unidade 301. Preencha-a na tela de unidades',
    ],
    [
      'fractions_missing',
      [unit('301'), unit('701')],
      2,
      'Faltam as frações ideais das unidades 301 e 701. Preencha-as na tela de unidades',
    ],
    [
      'fractions_missing',
      [unit('101', 'A'), unit('102', 'A'), unit('301')],
      3,
      'Faltam as frações ideais das unidades Bloco A · 101, Bloco A · 102 e 301. Preencha-as na tela de unidades',
    ],
    [
      'fractions_missing',
      [unit('301'), unit('302'), unit('303'), unit('304'), unit('305')],
      7,
      'Faltam as frações ideais de 7 unidades, entre elas 301, 302 e 303. Preencha-as na tela de unidades',
    ],
    [
      'areas_missing',
      [unit('301')],
      1,
      'Falta a área da unidade 301. Preencha-a na tela de unidades',
    ],
    [
      'areas_missing',
      [unit('301'), unit('701')],
      2,
      'Faltam as áreas das unidades 301 e 701. Preencha-as na tela de unidades',
    ],
    [
      'areas_missing',
      [unit('301'), unit('302'), unit('303'), unit('304')],
      4,
      'Faltam as áreas de 4 unidades, entre elas 301, 302 e 303. Preencha-as na tela de unidades',
    ],
  ])('nomeia o que falta (%s, %j unidades)', (reason, units, unitCount, expected) => {
    expect(describeApportionmentShortfall(shortfall({ reason, units, unitCount }))).toBe(expected);
  });

  it.each([
    ['sem dados', undefined],
    ['motivo desconhecido', { reason: 'algo_novo' }],
    ['soma que nao e numero', { reason: 'fractions_total', fractionTotal: 'x' }],
    ['faltas sem unidades', { reason: 'fractions_missing', units: [], unitCount: 0 }],
    ['unidades que nao sao uma lista', { reason: 'areas_missing', units: 'x', unitCount: 2 }],
    ['unidades sem numero', { reason: 'areas_missing', units: [{ block: null }, 7], unitCount: 2 }],
  ])('cai no texto geral quando vem %s', (_name, data) => {
    expect(describeApportionmentShortfall(shortfall(data))).toBe(GENERIC);
  });
});
