import { monthClosing, monthEntries, entry } from '../../testing/finance.fixtures';
import { paymentInstructions } from '../../testing/payment-instructions.fixtures';
import { unitsSummary } from '../../testing/units.fixtures';
import { PendingInput, buildPendings, monthState, receivedPercent } from './dashboard-values';

const TITLE = 'Novembro de 2026';

/** Tudo em ordem: nenhuma pendência. Cada teste estraga só o que quer provar. */
function healthy(overrides: Partial<PendingInput> = {}): PendingInput {
  return {
    month: monthEntries({ entries: [entry()], closing: monthClosing() }),
    overdueCharges: 0,
    units: unitsSummary({
      readiness: [
        { method: 'idealFraction', ready: true },
        { method: 'equalShare', ready: true },
        { method: 'area', ready: false },
      ],
    }),
    instructions: paymentInstructions(),
    ...overrides,
  };
}

function ids(input: PendingInput): string[] {
  return buildPendings(input).map((item) => item.id);
}

describe('monthState', () => {
  it('diz que o mês ainda não foi aberto quando não há lançamentos nem fechamento', () => {
    expect(monthState(monthEntries())).toBe('not-open');
  });

  it('diz que o mês está aberto quando há lançamentos e nenhum fechamento', () => {
    expect(monthState(monthEntries({ entries: [entry()] }))).toBe('open');
  });

  it('diz que o mês está fechado quando há fechamento', () => {
    expect(monthState(monthEntries({ entries: [entry()], closing: monthClosing() }))).toBe(
      'closed',
    );
  });
});

describe('receivedPercent', () => {
  it.each([
    [1000, 250, 25],
    [1000, 0, 0],
    [3, 1, 33],
    [1000, 1000, 100],
    [1000, 1200, 100],
    [0, 0, 0],
  ])('de %s cobrados e %s recebidos mostra %s%', (charged, received, expected) => {
    expect(receivedPercent(charged, received)).toBe(expected);
  });

  it('não mostra 100% enquanto faltam centavos', () => {
    expect(receivedPercent(1000, 999.99)).toBe(99);
  });
});

describe('buildPendings', () => {
  it('não aponta nada quando está tudo em ordem', () => {
    expect(buildPendings(healthy())).toEqual([]);
  });

  it('pede para cadastrar as unidades quando não há nenhuma ativa', () => {
    const [item] = buildPendings(healthy({ units: unitsSummary({ active: 0 }) }));

    expect(item).toEqual({
      id: 'no-units',
      text: 'Nenhuma unidade cadastrada',
      link: '/unidades',
      linkLabel: 'Cadastrar unidades',
    });
  });

  it('avisa que o rateio não está pronto, sem repetir o aviso das unidades', () => {
    const units = unitsSummary({
      readiness: [
        { method: 'idealFraction', ready: false },
        { method: 'equalShare', ready: true },
        { method: 'area', ready: true },
      ],
    });

    const [item] = buildPendings(healthy({ units }));

    expect(item).toEqual({
      id: 'apportionment',
      text: 'O rateio ainda não está pronto: faltam dados nas unidades',
      link: '/unidades',
      linkLabel: 'Completar unidades',
    });
    expect(ids(healthy({ units: unitsSummary({ ...units, active: 0 }) }))).toEqual(['no-units']);
  });

  it('só olha o método de rateio escolhido', () => {
    const units = unitsSummary({
      apportionmentMethod: 'equalShare',
      readiness: [
        { method: 'idealFraction', ready: false },
        { method: 'equalShare', ready: true },
        { method: 'area', ready: false },
      ],
    });

    expect(ids(healthy({ units }))).toEqual([]);
  });

  it('pede os dados de pagamento quando não há chave Pix nem conta', () => {
    const instructions = paymentInstructions({ pix: null, account: null });

    expect(buildPendings(healthy({ instructions }))).toEqual([
      {
        id: 'payment-instructions',
        text: 'Os dados de pagamento ainda não foram informados',
        link: '/financeiro/dados-de-pagamento',
        linkLabel: 'Informar dados',
      },
    ]);
  });

  it('aceita só a chave Pix ou só a conta como dados de pagamento', () => {
    expect(ids(healthy({ instructions: paymentInstructions({ account: null }) }))).toEqual([]);
    expect(ids(healthy({ instructions: paymentInstructions({ pix: null }) }))).toEqual([]);
  });

  it('pede para abrir o mês que ainda não foi aberto, com o nome do mês', () => {
    expect(buildPendings(healthy({ month: monthEntries() }))).toEqual([
      {
        id: 'month-not-open',
        text: `O mês de ${TITLE} ainda não foi aberto`,
        link: '/financeiro/lancamentos',
        linkLabel: 'Abrir o mês',
      },
    ]);
  });

  it.each([
    [1, '1 lançamento aguarda o valor'],
    [3, '3 lançamentos aguardam o valor'],
  ])('com %s lançamento(s) sem valor diz "%s"', (needsAmountCount, text) => {
    const month = monthEntries({ entries: [entry()], needsAmountCount });

    expect(buildPendings(healthy({ month }))).toEqual([
      {
        id: 'amounts-pending',
        text,
        link: '/financeiro/lancamentos',
        linkLabel: 'Informar valores',
      },
    ]);
  });

  it.each([
    [1, '1 despesa vencida sem pagamento'],
    [4, '4 despesas vencidas sem pagamento'],
  ])('com %s despesa(s) vencida(s) diz "%s"', (overdueCount, text) => {
    const month = monthEntries({ entries: [entry()], overdueCount });

    expect(buildPendings(healthy({ month }))).toEqual([
      {
        id: 'expenses-overdue',
        text,
        link: '/financeiro/lancamentos',
        linkLabel: 'Ver lançamentos',
      },
    ]);
  });

  it.each([
    [1, '1 cobrança em atraso'],
    [7, '7 cobranças em atraso'],
  ])('com %s cobrança(s) em atraso diz "%s"', (overdueCharges, text) => {
    expect(buildPendings(healthy({ overdueCharges }))).toEqual([
      {
        id: 'charges-overdue',
        text,
        link: '/financeiro/pagamentos',
        linkLabel: 'Ver pagamentos',
      },
    ]);
  });

  it('lista tudo na ordem de quem precisa agir primeiro', () => {
    const input: PendingInput = {
      month: monthEntries({ entries: [entry()], needsAmountCount: 2, overdueCount: 1 }),
      overdueCharges: 3,
      units: unitsSummary({ active: 0 }),
      instructions: paymentInstructions({ pix: null, account: null }),
    };

    expect(ids(input)).toEqual([
      'no-units',
      'payment-instructions',
      'amounts-pending',
      'expenses-overdue',
      'charges-overdue',
    ]);
  });

  it('ignora o que não foi lido, sem inventar pendência', () => {
    expect(
      buildPendings({ month: null, overdueCharges: null, units: null, instructions: null }),
    ).toEqual([]);
  });
});
