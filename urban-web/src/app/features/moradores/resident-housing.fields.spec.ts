import { startDateErrors } from './resident-housing.fields';

describe('startDateErrors', () => {
  const TODAY = new Date(2026, 10, 14);

  it('diz o que há de errado em cada recusa da data de entrada, com o limite exato', () => {
    expect(startDateErrors(() => TODAY)).toEqual({
      required: 'Informe a data de entrada',
      date: 'Informe uma data de entrada válida, como 01/03/2024',
      tooOld: 'A data de entrada não pode ser anterior a 01/01/1900',
      future: 'A data de entrada pode ser, no máximo, 14/11/2027 (1 ano à frente de hoje)',
    });
  });

  it('acompanha o dia de hoje', () => {
    expect(startDateErrors(() => new Date(2028, 0, 5)).future).toBe(
      'A data de entrada pode ser, no máximo, 05/01/2029 (1 ano à frente de hoje)',
    );
  });
});
