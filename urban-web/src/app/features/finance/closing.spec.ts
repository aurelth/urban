import { chargeLine } from '../../testing/finance.fixtures';
import { basisLabel, chargeShare, chargesTotal, formatCubicMeters, lineDetail } from './closing';

describe('basisLabel', () => {
  it('usa os mesmos nomes do metodo de rateio da tela de unidades', () => {
    expect(basisLabel('idealFraction')).toBe('Fração ideal');
    expect(basisLabel('equalShare')).toBe('Valor igual');
    expect(basisLabel('area')).toBe('Área');
  });
});

describe('chargeShare', () => {
  it.each([
    [0.6, '60%'],
    [0.0125, '1,25%'],
    [0.33333333, '33,333333%'],
  ])('mostra a fracao %s como "%s"', (weight, text) => {
    expect(chargeShare('idealFraction', weight)).toBe(text);
  });

  it.each([
    [55.5, '55,5 m²'],
    [80, '80 m²'],
    [1250.5, '1.250,5 m²'],
  ])('mostra a area %s como "%s"', (weight, text) => {
    expect(chargeShare('area', weight)).toBe(text);
  });

  it('diz que a parte e igual quando todas pagam o mesmo', () => {
    expect(chargeShare('equalShare', 1)).toBe('Parte igual');
  });
});

describe('formatCubicMeters', () => {
  it.each([
    [17.649, '17,649 m³'],
    [5, '5 m³'],
    [0.852, '0,852 m³'],
    [1250.5, '1.250,5 m³'],
  ])('mostra %s como "%s"', (value, text) => {
    expect(formatCubicMeters(value)).toBe(text);
  });
});

describe('lineDetail', () => {
  it('mostra o consumo e as leituras das linhas de consumo', () => {
    const water = chargeLine({
      kind: 'water',
      label: 'Água',
      quantity: 17.649,
      previousReading: 1551.913,
      currentReading: 1569.562,
    });

    expect(lineDetail(water)).toBe('17,649 m³ (leitura de 1.551,913 a 1.569,562)');
  });

  it('mostra so o consumo quando a linha nao guarda as leituras', () => {
    expect(lineDetail(chargeLine({ kind: 'gas', label: 'Gás', quantity: 5 }))).toBe('5 m³');
  });

  it('nao tem detalhe nas linhas sem consumo', () => {
    expect(lineDetail(chargeLine())).toBeNull();
    expect(lineDetail(chargeLine({ kind: 'reserveFund', label: 'Fundo de reserva' }))).toBeNull();
  });
});

describe('chargesTotal', () => {
  it('soma as cobrancas em centavos, sem sobra de ponto flutuante', () => {
    expect(chargesTotal([{ amount: 803.44 }, { amount: 507.43 }])).toBe(1310.87);
    expect(chargesTotal([{ amount: 0.1 }, { amount: 0.2 }])).toBe(0.3);
  });

  it('e zero sem cobrancas', () => {
    expect(chargesTotal([])).toBe(0);
  });
});
