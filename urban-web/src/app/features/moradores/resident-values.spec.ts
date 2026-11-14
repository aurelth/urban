import { occupancy, resident } from '../../testing/residents.fixtures';
import {
  formatPhone,
  isCurrentResident,
  kindLabel,
  occupancyPeriod,
  occupancyUnit,
} from './resident-values';

describe('isCurrentResident', () => {
  it('é morador atual quem tem ao menos um vínculo em andamento', () => {
    const lives = resident({
      occupancies: [occupancy({ endDate: '2025-01-31' }), occupancy({ endDate: null })],
    });

    expect(isCurrentResident(lives)).toBe(true);
  });

  it('é ex-morador quem só tem vínculos encerrados', () => {
    const left = resident({ occupancies: [occupancy({ endDate: '2025-01-31' })] });

    expect(isCurrentResident(left)).toBe(false);
  });
});

describe('occupancyUnit', () => {
  it('mostra bloco e número', () => {
    expect(occupancyUnit(occupancy({ unitBlock: 'B', unitNumber: '202' }))).toBe('Bloco B · 202');
  });

  it('mostra só o número quando a unidade não tem bloco', () => {
    expect(occupancyUnit(occupancy({ unitBlock: null, unitNumber: '15' }))).toBe('15');
  });
});

describe('kindLabel', () => {
  it('traduz o tipo de vínculo', () => {
    expect(kindLabel('owner')).toBe('Proprietário');
    expect(kindLabel('tenant')).toBe('Inquilino');
    expect(kindLabel('dependent')).toBe('Dependente');
  });
});

describe('occupancyPeriod', () => {
  it('diz desde quando mora quem ainda mora', () => {
    expect(occupancyPeriod(occupancy({ startDate: '2024-03-01', endDate: null }))).toBe(
      'desde 01/03/2024',
    );
  });

  it('diz de quando a quando morou quem já saiu', () => {
    expect(occupancyPeriod(occupancy({ startDate: '2024-03-01', endDate: '2026-10-31' }))).toBe(
      'de 01/03/2024 a 31/10/2026',
    );
  });
});

describe('formatPhone', () => {
  it('formata celular com DDD', () => {
    expect(formatPhone('47998765432')).toBe('(47) 99876-5432');
  });

  it('formata telefone fixo com DDD', () => {
    expect(formatPhone('4733334444')).toBe('(47) 3333-4444');
  });

  it('devolve o texto como veio quando não é um telefone com DDD', () => {
    expect(formatPhone('123')).toBe('123');
    expect(formatPhone('')).toBe('');
  });
});
