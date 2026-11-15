import { dateFromText, textFromDate } from './date-text';

describe('dateFromText', () => {
  it('lê dd/mm/aaaa como data local, sem escorregar de dia', () => {
    const date = dateFromText('05/03/2024')!;

    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2024, 2, 5]);
    expect([date.getHours(), date.getMinutes()]).toEqual([0, 0]);
  });

  it('aceita 29 de fevereiro só em ano bissexto', () => {
    expect(dateFromText('29/02/2024')).not.toBeNull();
    expect(dateFromText('29/02/2023')).toBeNull();
  });

  it.each([
    '',
    '5/3/2024',
    '05-03-2024',
    '32/01/2024',
    '00/01/2024',
    '10/13/2024',
    '05/03/24',
    'abc',
  ])('devolve nulo para "%s", que não é uma data completa e real', (text) => {
    expect(dateFromText(text)).toBeNull();
  });

  it('mantém o ano com quatro dígitos mesmo abaixo de 100 (o JavaScript o trataria como 19xx)', () => {
    expect(dateFromText('01/01/0099')!.getFullYear()).toBe(99);
  });
});

describe('textFromDate', () => {
  it('escreve dd/mm/aaaa com zeros à esquerda', () => {
    expect(textFromDate(new Date(2024, 2, 5))).toBe('05/03/2024');
    expect(textFromDate(new Date(2026, 10, 15))).toBe('15/11/2026');
  });

  it('usa quatro dígitos no ano', () => {
    const date = new Date(2000, 0, 1);
    date.setFullYear(99);

    expect(textFromDate(date)).toBe('01/01/0099');
  });

  it('é o inverso de dateFromText', () => {
    expect(textFromDate(dateFromText('29/02/2024')!)).toBe('29/02/2024');
  });
});
