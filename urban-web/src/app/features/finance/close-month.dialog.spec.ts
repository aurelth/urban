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
import { CloseFailure, openCloseMonthDialog } from './close-month.dialog';

const TAG = 'urban-close-month-dialog';

type Save = jest.Mock<(dueDate: string) => Promise<CloseFailure | null>>;

function setup(): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(dueDate: string) => Promise<CloseFailure | null>>()
    .mockResolvedValue(null);
  const result = openCloseMonthDialog(TestBed.inject(Injector), {
    month: '2026-11',
    total: 4200,
    save,
  });
  TestBed.tick();
  return { save, result };
}

function readingsLinkOf(): HTMLAnchorElement | null {
  return dialogOf(TAG).querySelector<HTMLAnchorElement>('[data-testid="readings-pending-link"]');
}

describe('CloseMonthDialog', () => {
  afterEach(removeOverlays);

  it('diz que mes vai fechar, o total que sera dividido e o que muda depois', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Fechar Novembro de 2026');
    const text = (dialogOf(TAG).textContent ?? '').replaceAll('\u00a0', ' ');
    expect(text).toContain('R$ 4.200,00');
    expect(text).toContain('dividido entre as unidades');
    expect(text).toContain('valores e os cancelamentos do mês ficam travados');
    expect(text).toContain('reabrir o mês');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Fechar mês');
  });

  it('sugere o dia 10 do mes seguinte e fecha com ele de uma vez', async () => {
    const { save, result } = setup();

    expect(fieldOf(TAG, 'dueDate').value).toBe('10/12/2026');

    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith('2026-12-10');
    expect(await result).toBe(true);
  });

  it('fecha com a data que a sindica escolheu', async () => {
    const { save } = setup();

    typeIn(TAG, 'dueDate', '15/12/2026');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith('2026-12-15');
  });

  it.each(['', '31/02/2027', '31/10/2026', '10/12'])('nao fecha com "%s" e explica', (text) => {
    const { save } = setup();

    typeIn(TAG, 'dueDate', text);
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe uma data válida, a partir de 01/11/2026']);
  });

  it('continua aberto com o erro quando o fechamento e recusado', async () => {
    const { save } = setup();
    save.mockResolvedValue({ message: 'Complete os dados das unidades' });

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Complete os dados das unidades');
    expect(fieldOf(TAG, 'dueDate').value).toBe('10/12/2026');
    expect(readingsLinkOf()).toBeNull();
  });

  it('leva as leituras do mes quando falta leitura de alguma unidade', async () => {
    const { save } = setup();
    save.mockResolvedValue({
      message: 'Informe as leituras do mês antes de fechar',
      readingsPending: true,
    });

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Informe as leituras do mês antes de fechar');
    expect(readingsLinkOf()?.textContent?.trim()).toBe('Informar as leituras do mês');
    expect(readingsLinkOf()?.getAttribute('href')).toBe('/financeiro/leituras?mes=2026-11');
    expect(fieldOf(TAG, 'dueDate').value).toBe('10/12/2026');
  });

  it('fecha o dialogo ao seguir para as leituras, sem fechar o mes', async () => {
    const { save, result } = setup();
    save.mockResolvedValue({
      message: 'Informe as leituras do mês antes de fechar',
      readingsPending: true,
    });

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();
    readingsLinkOf()!.click();
    await flushPromises();
    TestBed.tick();

    await expect(result).resolves.toBe(false);
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('tira o link das leituras quando o proximo erro e outro', async () => {
    const { save } = setup();
    save.mockResolvedValueOnce({ message: 'Faltam leituras', readingsPending: true });
    save.mockResolvedValueOnce({ message: 'Complete os dados das unidades' });

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();
    expect(readingsLinkOf()).not.toBeNull();

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Complete os dados das unidades');
    expect(readingsLinkOf()).toBeNull();
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
