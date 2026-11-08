import { basisLabel, chargeShare } from './closing';

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
