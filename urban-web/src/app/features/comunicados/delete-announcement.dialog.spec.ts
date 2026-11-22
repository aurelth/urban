import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { announcement } from '../../testing/announcements.fixtures';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
} from '../../testing/dialog.harness';
import { openDeleteAnnouncementDialog } from './delete-announcement.dialog';

const TAG = 'urban-delete-announcement-dialog';

type Save = jest.Mock<() => Promise<string | null>>;

function setup(): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<() => Promise<string | null>>().mockResolvedValue(null);
  const result = openDeleteAnnouncementDialog(TestBed.inject(Injector), {
    announcement: announcement({ title: 'Reunião de sábado' }),
    save,
  });
  TestBed.tick();
  return { save, result };
}

describe('DeleteAnnouncementDialog', () => {
  afterEach(removeOverlays);

  it('diz qual comunicado vai ser excluído e que ele some do mural junto com as leituras', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Excluir Reunião de sábado',
    );
    expect(dialogOf(TAG).textContent).toContain('some do mural dos moradores');
    expect(dialogOf(TAG).textContent).toContain('quem já leu');
    expect(dialogOf(TAG).textContent).toContain('não se desfaz');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Excluir comunicado');
  });

  it('exclui e fecha', async () => {
    const { save, result } = setup();

    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledTimes(1);
    await expect(result).resolves.toBe(true);
  });

  it('continua aberto com o erro quando a API recusa', async () => {
    const { save } = setup();
    save.mockResolvedValue('Este comunicado já foi apagado');

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Este comunicado já foi apagado');
    expect(dialogOf(TAG)).not.toBeNull();
  });

  it('trava os botões e troca o texto enquanto exclui', async () => {
    const { save } = setup();
    let finish: (message: string | null) => void = () => undefined;
    save.mockReturnValue(new Promise<string | null>((resolve) => (finish = resolve)));

    submitDialog(TAG);
    TestBed.tick();

    expect(submitButtonOf(TAG).disabled).toBe(true);
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Excluindo');
    expect(dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')?.disabled).toBe(
      true,
    );
    finish(null);
    await flushPromises();
  });

  it('cancelar fecha sem excluir', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    await expect(result).resolves.toBe(false);
    expect(save).not.toHaveBeenCalled();
  });

  it('o botão de excluir é vermelho e sem fundo, como os outros de apagar', () => {
    setup();

    expect(submitButtonOf(TAG).classList.contains('urban-danger')).toBe(true);
  });
});
