import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { ConsumptionTariff, ConsumptionTariffPayload } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  checkboxOf,
  chooseIn,
  choiceOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  toggleIn,
  typeIn,
} from '../../testing/dialog.harness';
import { consumptionTariff, tariffBand } from '../../testing/finance.fixtures';
import { TariffDialogConfig, openTariffDialog } from './tariff.dialog';

const TAG = 'urban-tariff-dialog';

type Save = jest.Mock<(month: string, payload: ConsumptionTariffPayload) => Promise<string | null>>;

interface Options {
  tariff?: ConsumptionTariff | null;
  takenMonths?: string[];
}

function setup(options: Options = {}): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(month: string, payload: ConsumptionTariffPayload) => Promise<string | null>>()
    .mockResolvedValue(null);
  const config: TariffDialogConfig = {
    tariff: options.tariff ?? null,
    takenMonths: options.takenMonths ?? [],
    month: '2026-11',
    save,
  };
  const result = openTariffDialog(TestBed.inject(Injector), config);
  TestBed.tick();
  return { save, result };
}

type Section = 'water' | 'sewage';

function rows(section: Section): HTMLElement[] {
  return [
    ...dialogOf(TAG).querySelectorAll<HTMLElement>(
      `[data-testid="${section}-bands"] [data-testid="band-row"]`,
    ),
  ];
}

function bandInput(section: Section, row: number, name: string): HTMLInputElement {
  return rows(section)[row].querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
}

function typeBand(section: Section, row: number, name: string, value: string): void {
  const input = bandInput(section, row, name);
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function click(section: Section, testId: string, row = 0): void {
  const scope = dialogOf(TAG).querySelector(`[data-testid="${section}-bands"]`)!;
  scope.querySelectorAll<HTMLButtonElement>(`[data-testid="${testId}"]`)[row].click();
  TestBed.tick();
}

function plain(value: string | null | undefined): string {
  return (value ?? '').replace(/\s+/g, ' ').replaceAll(' ', ' ').trim();
}

/** Água de uma faixa só, pronta para gravar. */
function fillSimpleWater(): void {
  typeBand('water', 0, 'fixed', '28,41');
  typeBand('water', 0, 'price', '5,5');
}

async function submit(): Promise<void> {
  submitDialog(TAG);
  await flushPromises();
  TestBed.tick();
}

describe('TariffDialog', () => {
  afterEach(removeOverlays);

  describe('tarifa nova', () => {
    it('mostra o titulo, o mes de hoje e uma faixa de agua sem limite', () => {
      setup();

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Nova tarifa de consumo');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar tarifa');
      expect(choiceOf(TAG, 'month')).toBe('2026-11');
      expect(rows('water')).toHaveLength(1);
      expect(bandInput('water', 0, 'upTo').value).toBe('Sem limite');
      expect(bandInput('water', 0, 'upTo').disabled).toBe(true);
      expect(bandInput('water', 0, 'fixed').value).toBe('0,00');
    });

    it('comeca sem esgoto e sem gas', () => {
      setup();

      expect(checkboxOf(TAG, 'sewage-enabled').checked).toBe(false);
      expect(checkboxOf(TAG, 'gas-enabled').checked).toBe(false);
      expect(dialogOf(TAG).querySelector('[data-testid="sewage-bands"]')).toBeNull();
      expect(dialogOf(TAG).querySelector('[formControlName="kilos"]')).toBeNull();
    });

    it('grava so a agua quando esgoto e gas ficam desligados', async () => {
      const { save, result } = setup();

      fillSimpleWater();
      await submit();

      expect(save).toHaveBeenCalledWith('2026-11', {
        water: [{ upToCubicMeters: null, fixedAmount: 28.41, pricePerCubicMeter: 5.5 }],
        sewage: null,
        sewagePercent: 100,
        gas: null,
      });
      expect(await result).toBe(true);
    });

    it('permite escolher outro mes de inicio', async () => {
      const { save } = setup();

      chooseIn(TAG, 'month', '2026-12');
      fillSimpleWater();
      await submit();

      expect(save).toHaveBeenCalledWith('2026-12', expect.anything());
    });

    it('recusa um mes que ja tem tarifa e manda editar a existente', async () => {
      const { save } = setup({ takenMonths: ['2026-11', '2026-03'] });

      fillSimpleWater();
      await submit();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual([
        'Já existe uma tarifa que começa neste mês. Edite a tarifa dela na lista',
      ]);
    });

    it('explica o que falta em cada campo da faixa', async () => {
      const { save } = setup();

      await submit();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual([
        'Informe o preço por m³, com até 4 casas (use 0 se não houver)',
      ]);

      typeBand('water', 0, 'fixed', 'abc');
      await submit();

      expect(errorsOf(TAG)).toContain(
        'Informe o valor fixo, com até 2 casas (use 0 se não houver)',
      );
    });
  });

  describe('faixas', () => {
    it('adiciona faixas, com o limite na que deixou de ser a ultima, e grava em ordem', async () => {
      const { save } = setup();

      click('water', 'add-band');
      expect(rows('water')).toHaveLength(2);
      expect(bandInput('water', 0, 'upTo').disabled).toBe(false);
      expect(bandInput('water', 0, 'upTo').value).toBe('');
      expect(bandInput('water', 1, 'upTo').value).toBe('Sem limite');

      typeBand('water', 0, 'upTo', '10');
      typeBand('water', 0, 'fixed', '28,41');
      typeBand('water', 0, 'price', '0');
      typeBand('water', 1, 'price', '5,1');
      await submit();

      expect(save).toHaveBeenCalledWith('2026-11', {
        water: [
          { upToCubicMeters: 10, fixedAmount: 28.41, pricePerCubicMeter: 0 },
          { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.1 },
        ],
        sewage: null,
        sewagePercent: 100,
        gas: null,
      });
    });

    it('nao deixa passar de 6 faixas', () => {
      setup();

      for (let index = 0; index < 5; index++) {
        click('water', 'add-band');
      }

      expect(rows('water')).toHaveLength(6);
      const add = dialogOf(TAG).querySelector<HTMLButtonElement>(
        '[data-testid="water-bands"] [data-testid="add-band"]',
      )!;
      expect(add.disabled).toBe(true);
    });

    it('remove uma faixa e nao deixa remover a unica', () => {
      setup();
      click('water', 'add-band');
      expect(rows('water')).toHaveLength(2);

      click('water', 'remove-band', 0);

      expect(rows('water')).toHaveLength(1);
      expect(bandInput('water', 0, 'upTo').value).toBe('Sem limite');
      const remove = rows('water')[0].querySelector<HTMLButtonElement>(
        '[data-testid="remove-band"]',
      )!;
      expect(remove.disabled).toBe(true);
    });

    it('exige limites crescentes e avisa com texto', async () => {
      const { save } = setup();
      click('water', 'add-band');
      click('water', 'add-band');

      typeBand('water', 0, 'upTo', '20');
      typeBand('water', 1, 'upTo', '10');
      typeBand('water', 0, 'price', '1');
      typeBand('water', 1, 'price', '2');
      typeBand('water', 2, 'price', '3');
      await submit();

      expect(save).not.toHaveBeenCalled();
      expect(
        plain(dialogOf(TAG).querySelector('[data-testid="water-ascending-error"]')?.textContent),
      ).toBe('Cada limite deve ser maior que o da faixa anterior');
    });

    it('recusa limite com mais de 3 casas ou igual a zero', async () => {
      const { save } = setup();
      click('water', 'add-band');

      typeBand('water', 0, 'upTo', '0');
      typeBand('water', 0, 'price', '1');
      typeBand('water', 1, 'price', '2');
      await submit();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual(['Informe o limite em m³, maior que zero, com até 3 casas']);
    });
  });

  describe('esgoto', () => {
    it('liga as faixas do esgoto com o percentual de 100', () => {
      setup();

      toggleIn(TAG, 'sewage-enabled');
      TestBed.tick();

      expect(rows('sewage')).toHaveLength(1);
      expect(fieldOf(TAG, 'sewagePercent').value).toBe('100');
    });

    it('copia as faixas da agua para o esgoto', () => {
      setup({ tariff: consumptionTariff({ validFrom: '2026-03' }) });
      toggleIn(TAG, 'sewage-enabled');
      TestBed.tick();
      expect(rows('sewage')).toHaveLength(1);

      dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="copy-water-bands"]')!.click();
      TestBed.tick();

      expect(rows('sewage')).toHaveLength(3);
      expect(bandInput('sewage', 0, 'upTo').value).toBe('10');
      expect(bandInput('sewage', 0, 'fixed').value).toBe('28,41');
      expect(bandInput('sewage', 1, 'price').value).toBe('3,50');
      expect(bandInput('sewage', 2, 'upTo').value).toBe('Sem limite');
    });

    it('grava as faixas do esgoto e o percentual cobrado', async () => {
      const { save } = setup();

      fillSimpleWater();
      toggleIn(TAG, 'sewage-enabled');
      TestBed.tick();
      typeBand('sewage', 0, 'fixed', '10');
      typeBand('sewage', 0, 'price', '4,25');
      typeIn(TAG, 'sewagePercent', '50');
      await submit();

      expect(save).toHaveBeenCalledWith('2026-11', {
        water: [{ upToCubicMeters: null, fixedAmount: 28.41, pricePerCubicMeter: 5.5 }],
        sewage: [{ upToCubicMeters: null, fixedAmount: 10, pricePerCubicMeter: 4.25 }],
        sewagePercent: 50,
        gas: null,
      });
    });

    it('recusa percentual fora de 0 a 100', async () => {
      const { save } = setup();

      fillSimpleWater();
      toggleIn(TAG, 'sewage-enabled');
      TestBed.tick();
      typeBand('sewage', 0, 'price', '4');
      typeIn(TAG, 'sewagePercent', '150');
      await submit();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual(['O percentual vai de 0 a 100, com até 2 casas']);
    });

    it('desligar o esgoto de novo tira o esgoto da tarifa, mesmo com campos invalidos', async () => {
      const { save } = setup();

      fillSimpleWater();
      toggleIn(TAG, 'sewage-enabled');
      TestBed.tick();
      toggleIn(TAG, 'sewage-enabled');
      TestBed.tick();
      await submit();

      expect(save).toHaveBeenCalledWith(
        '2026-11',
        expect.objectContaining({ sewage: null, sewagePercent: 100 }),
      );
    });
  });

  describe('gas', () => {
    it('mostra o preco do m3 calculado em kg por m3 vezes o preco do kg', () => {
      setup();

      toggleIn(TAG, 'gas-enabled');
      TestBed.tick();
      typeIn(TAG, 'kilos', '2,4');
      typeIn(TAG, 'pricePerKilo', '15,71');
      TestBed.tick();

      expect(plain(dialogOf(TAG).querySelector('[data-testid="gas-price"]')?.textContent)).toBe(
        'Preço do m³: R$ 37,70',
      );
    });

    it('grava os kg por m3 e o preco do kg', async () => {
      const { save } = setup();

      fillSimpleWater();
      toggleIn(TAG, 'gas-enabled');
      TestBed.tick();
      typeIn(TAG, 'kilos', '2,4');
      typeIn(TAG, 'pricePerKilo', '15,71');
      await submit();

      expect(save).toHaveBeenCalledWith('2026-11', {
        water: [{ upToCubicMeters: null, fixedAmount: 28.41, pricePerCubicMeter: 5.5 }],
        sewage: null,
        sewagePercent: 100,
        gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71 },
      });
    });

    it('explica o que falta no gas', async () => {
      const { save } = setup();

      fillSimpleWater();
      toggleIn(TAG, 'gas-enabled');
      TestBed.tick();
      await submit();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual([
        'Informe os kg por m³, maior que zero, com até 3 casas',
        'Informe o preço do kg, maior que zero, com até 2 casas',
      ]);
    });
  });

  describe('editar', () => {
    const TARIFF = consumptionTariff({
      validFrom: '2026-03',
      sewage: [tariffBand({ upToCubicMeters: 10, fixedAmount: 12.5 }), tariffBand()],
      sewagePercent: 50,
      gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71, pricePerCubicMeter: 37.7 },
    });

    it('mostra o mes fixo, as faixas, o esgoto e o gas da tarifa', () => {
      setup({ tariff: TARIFF, takenMonths: ['2026-03'] });

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
        'Editar tarifa de março de 2026',
      );
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar alterações');
      expect(dialogOf(TAG).querySelector('[formControlName="month"]')).toBeNull();
      expect(rows('water')).toHaveLength(3);
      expect(bandInput('water', 0, 'upTo').value).toBe('10');
      expect(bandInput('water', 0, 'fixed').value).toBe('28,41');
      expect(checkboxOf(TAG, 'sewage-enabled').checked).toBe(true);
      expect(rows('sewage')).toHaveLength(2);
      expect(bandInput('sewage', 0, 'fixed').value).toBe('12,50');
      expect(fieldOf(TAG, 'sewagePercent').value).toBe('50');
      expect(checkboxOf(TAG, 'gas-enabled').checked).toBe(true);
      expect(fieldOf(TAG, 'kilos').value).toBe('2,4');
      expect(fieldOf(TAG, 'pricePerKilo').value).toBe('15,71');
    });

    it('grava no proprio mes, sem tratar a tarifa que edita como repetida', async () => {
      const { save, result } = setup({ tariff: TARIFF, takenMonths: ['2026-03'] });

      typeBand('water', 2, 'price', '6');
      await submit();

      expect(save).toHaveBeenCalledWith('2026-03', {
        water: [
          { upToCubicMeters: 10, fixedAmount: 28.41, pricePerCubicMeter: 0 },
          { upToCubicMeters: 20, fixedAmount: 0, pricePerCubicMeter: 3.5 },
          { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 6 },
        ],
        sewage: [
          { upToCubicMeters: 10, fixedAmount: 12.5, pricePerCubicMeter: 5.1 },
          { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.1 },
        ],
        sewagePercent: 50,
        gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71 },
      });
      expect(await result).toBe(true);
    });

    it('tirar o gas e gravar manda a tarifa sem gas', async () => {
      const { save } = setup({ tariff: TARIFF, takenMonths: ['2026-03'] });

      toggleIn(TAG, 'gas-enabled');
      TestBed.tick();
      await submit();

      expect(save).toHaveBeenCalledWith('2026-03', expect.objectContaining({ gas: null }));
    });
  });

  it('continua aberto com o erro e os campos quando a API recusa', async () => {
    const { save } = setup();
    save.mockResolvedValue('As faixas precisam ser de 1 a 6');

    fillSimpleWater();
    await submit();

    expect(alertOf(TAG)).toBe('As faixas precisam ser de 1 a 6');
    expect(bandInput('water', 0, 'price').value).toBe('5,5');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
