import { consumptionTariff, tariffBand } from '../../testing/finance.fixtures';
import { bandsText, inForceMonth, monthName, reserveFundText, tariffLines } from './tariff-values';

function plain(value: string): string {
  return value.replaceAll('\u00a0', ' ');
}

describe('monthName', () => {
  it('writes the month in lower case, for use inside a sentence', () => {
    expect(monthName('2026-11')).toBe('novembro de 2026');
    expect(monthName('2027-03')).toBe('março de 2027');
  });
});

describe('bandsText', () => {
  it('counts the bands', () => {
    expect(bandsText(1)).toBe('1 faixa');
    expect(bandsText(3)).toBe('3 faixas');
  });
});

describe('tariffLines', () => {
  it('shows only the water when the tariff has no sewage and no gas', () => {
    expect(tariffLines(consumptionTariff())).toEqual([
      { label: 'Água', text: '3 faixas' },
      { label: 'Esgoto', text: 'Não cobrado' },
      { label: 'Gás', text: 'Não cobrado' },
    ]);
  });

  it('shows the sewage bands and how much of them is charged', () => {
    const lines = tariffLines(
      consumptionTariff({
        sewage: [tariffBand({ upToCubicMeters: 10 }), tariffBand()],
        sewagePercent: 50,
      }),
    );

    expect(lines[1]).toEqual({ label: 'Esgoto', text: '2 faixas, cobrando 50% do valor' });
  });

  it('shows the gas as kilos, price per kilo and price per cubic meter', () => {
    const lines = tariffLines(
      consumptionTariff({
        gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71, pricePerCubicMeter: 37.7 },
      }),
    );

    expect(plain(lines[2].text)).toBe('2,4 kg/m³ × R$ 15,71 por kg = R$ 37,70 por m³');
  });
});

describe('reserveFundText', () => {
  it('explains that there is no reserve fund at zero', () => {
    expect(reserveFundText(0)).toBe('Sem fundo de reserva no recibo');
  });

  it('writes the percent over the base groups', () => {
    expect(reserveFundText(5)).toBe('5% das despesas dos grupos marcados como base');
    expect(reserveFundText(2.5)).toBe('2,5% das despesas dos grupos marcados como base');
  });
});

describe('inForceMonth', () => {
  const tariffs = [
    consumptionTariff({ validFrom: '2027-01' }),
    consumptionTariff({ validFrom: '2026-11' }),
    consumptionTariff({ validFrom: '2026-03' }),
  ];

  it('is the newest tariff that already started', () => {
    expect(inForceMonth(tariffs, '2026-12')).toBe('2026-11');
    expect(inForceMonth(tariffs, '2027-01')).toBe('2027-01');
    expect(inForceMonth(tariffs, '2026-03')).toBe('2026-03');
  });

  it('is nothing before the first tariff', () => {
    expect(inForceMonth(tariffs, '2026-02')).toBeNull();
    expect(inForceMonth([], '2026-11')).toBeNull();
  });
});
