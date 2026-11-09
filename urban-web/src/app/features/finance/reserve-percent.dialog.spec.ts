import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { openReservePercentDialog } from './reserve-percent.dialog';

const TAG = 'urban-reserve-percent-dialog';

type Save = jest.Mock<(percent: number) => Promise<string | null>>;

function setup(percent = 5): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<(percent: number) => Promise<string | null>>().mockResolvedValue(null);
  const result = openReservePercentDialog(TestBed.inject(Injector), { percent, save });
  TestBed.tick();
  return { save, result };
}

describe('ReservePercentDialog', () => {
  afterEach(removeOverlays);

  it('mostra o percentual atual, explica e so habilita o botao depois de mudar', () => {
    setup(2.5);

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Percentual do fundo de reserva',
    );
    expect(fieldOf(TAG, 'percent').value).toBe('2,5');
    expect(dialogOf(TAG).textContent).toContain('0 tira o fundo de reserva do recibo');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar alterações');
    expect(submitButtonOf(TAG).disabled).toBe(true);

    typeIn(TAG, 'percent', '3');
    TestBed.tick();

    expect(submitButtonOf(TAG).disabled).toBe(false);
  });

  it.each([
    ['2,5', 2.5],
    ['10', 10],
    ['0', 0],
    ['100', 100],
    ['0,01', 0.01],
  ])('grava "%s" como %f e fecha', async (text, expected) => {
    const { save, result } = setup();

    typeIn(TAG, 'percent', text);
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith(expected);
    expect(await result).toBe(true);
  });

  it.each(['', 'abc', '100,01', '-1', '1,234'])('recusa "%s" e explica', (text) => {
    const { save } = setup();

    typeIn(TAG, 'percent', text);
    TestBed.tick();
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe um percentual de 0 a 100, com até 2 casas']);
  });

  it('continua aberto com o erro quando a API recusa', async () => {
    const { save } = setup();
    save.mockResolvedValue('O percentual do fundo de reserva vai de 0 a 100');

    typeIn(TAG, 'percent', '7');
    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('O percentual do fundo de reserva vai de 0 a 100');
    expect(fieldOf(TAG, 'percent').value).toBe('7');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
