import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { jest } from '@jest/globals';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { flushPromises } from '../../../testing/async';
import { PasswordConfirmConfig, openPasswordConfirm } from './password-confirm.dialog';

type Run = jest.Mock<(password: string) => Promise<string | null>>;

function setup(overrides: Partial<PasswordConfirmConfig> = {}): {
  run: Run;
  result: Promise<boolean>;
} {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const run: Run = jest.fn<(password: string) => Promise<string | null>>().mockResolvedValue(null);
  const result = openPasswordConfirm(TestBed.inject(Injector), {
    title: 'Confirme com a sua senha',
    description: 'Para alterar o CPF, informe a senha da sua conta',
    confirmLabel: 'Confirmar',
    busyLabel: 'Confirmando',
    run,
    ...overrides,
  });
  TestBed.tick();
  return { run, result };
}

function dialog(): HTMLElement | null {
  return document.querySelector('urban-password-confirm-dialog');
}

function input(): HTMLInputElement {
  return dialog()!.querySelector<HTMLInputElement>('input[type="password"], input[type="text"]')!;
}

function type(value: string): void {
  input().value = value;
  input().dispatchEvent(new Event('input'));
}

function submit(): void {
  dialog()!.querySelector('form')!.dispatchEvent(new Event('submit'));
}

function submitButton(): HTMLButtonElement {
  return dialog()!.querySelector<HTMLButtonElement>('button[type="submit"]')!;
}

function alertText(): string {
  return dialog()?.querySelector('[role="alert"]')?.textContent?.trim() ?? '';
}

async function settleDialog(): Promise<void> {
  await flushPromises();
  TestBed.tick();
  await flushPromises();
  TestBed.tick();
}

describe('openPasswordConfirm', () => {
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
  });

  it('pede a senha, explica por que e oferece confirmar ou cancelar', () => {
    setup();

    expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Confirme com a sua senha');
    expect(dialog()?.textContent).toContain('Para alterar o CPF, informe a senha da sua conta');
    expect(input().getAttribute('autocomplete')).toBe('current-password');
    expect(submitButton().textContent?.trim()).toBe('Confirmar');
    expect(dialog()?.querySelector('[data-testid="cancel"]')?.textContent?.trim()).toBe('Cancelar');
  });

  it('e modal e nao fecha ao clicar fora, para ninguem perder o que digitou sem querer', () => {
    setup();

    document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
    TestBed.tick();

    expect(dialog()).not.toBeNull();
  });

  it('sem senha nao chama a acao e pede a senha', async () => {
    const { run } = setup();

    submit();
    await settleDialog();

    expect(run).not.toHaveBeenCalled();
    expect(dialog()?.querySelector('mat-error')?.textContent?.trim()).toBe('Informe a senha');
  });

  it('entrega a senha digitada a acao', async () => {
    const { run } = setup();
    type('Senha-segura-123');

    submit();
    await settleDialog();

    expect(run).toHaveBeenCalledTimes(1);
    expect(run).toHaveBeenCalledWith('Senha-segura-123');
  });

  it('desabilita o botao e troca o texto enquanto a acao esta em andamento', async () => {
    let finish!: (message: string | null) => void;
    const { run } = setup();
    run.mockReturnValue(new Promise((resolve) => (finish = resolve)));
    type('Senha-segura-123');

    submit();
    TestBed.tick();

    expect(submitButton().disabled).toBe(true);
    expect(submitButton().textContent?.trim()).toBe('Confirmando');

    finish(null);
    await settleDialog();
  });

  it('nao envia duas vezes se o formulario for enviado de novo durante a acao', async () => {
    let finish!: (message: string | null) => void;
    const { run } = setup();
    run.mockReturnValue(new Promise((resolve) => (finish = resolve)));
    type('Senha-segura-123');

    submit();
    submit();
    TestBed.tick();

    expect(run).toHaveBeenCalledTimes(1);
    finish(null);
    await settleDialog();
  });

  it('fecha e responde que deu certo quando a acao nao devolve erro', async () => {
    const { result } = setup();
    type('Senha-segura-123');

    submit();
    await settleDialog();

    await expect(result).resolves.toBe(true);
    expect(dialog()).toBeNull();
  });

  it('mostra o erro dentro do diálogo, mantem aberto e limpa a senha para tentar de novo', async () => {
    const { run } = setup();
    run.mockResolvedValueOnce('Senha incorreta');
    type('Senha-errada-999');

    submit();
    await settleDialog();

    expect(dialog()).not.toBeNull();
    expect(alertText()).toBe('Senha incorreta');
    expect(input().value).toBe('');
    expect(submitButton().disabled).toBe(false);
    expect(submitButton().textContent?.trim()).toBe('Confirmar');
  });

  it('depois de um erro, uma nova tentativa certa fecha o diálogo', async () => {
    const { run, result } = setup();
    run.mockResolvedValueOnce('Senha incorreta');
    type('Senha-errada-999');
    submit();
    await settleDialog();

    type('Senha-segura-123');
    submit();
    await settleDialog();

    expect(run).toHaveBeenLastCalledWith('Senha-segura-123');
    await expect(result).resolves.toBe(true);
    expect(dialog()).toBeNull();
  });

  it('tira o erro anterior ao tentar de novo', async () => {
    const { run } = setup();
    let finish!: (message: string | null) => void;
    run.mockResolvedValueOnce('Senha incorreta');
    type('Senha-errada-999');
    submit();
    await settleDialog();
    run.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));

    type('Senha-segura-123');
    submit();
    TestBed.tick();

    expect(alertText()).toBe('');
    finish('Outro erro');
    await settleDialog();
  });

  it('cancelar fecha sem chamar a acao e responde que nao foi feito', async () => {
    const { run, result } = setup();
    type('Senha-segura-123');

    dialog()!.querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
    await settleDialog();

    await expect(result).resolves.toBe(false);
    expect(run).not.toHaveBeenCalled();
    expect(dialog()).toBeNull();
  });

  it('nao deixa a senha para a proxima vez que o diálogo abrir', async () => {
    setup();
    type('Senha-segura-123');
    submit();
    await settleDialog();
    expect(dialog()).toBeNull();

    openPasswordConfirm(TestBed.inject(Injector), {
      title: 'Confirme com a sua senha',
      description: 'de novo',
      confirmLabel: 'Confirmar',
      busyLabel: 'Confirmando',
      run: jest.fn<(password: string) => Promise<string | null>>().mockResolvedValue(null),
    });
    TestBed.tick();

    expect(input().value).toBe('');
    expect(document.body.innerHTML).not.toContain('Senha-segura-123');
  });

  it('a senha nunca aparece como texto da pagina nem como atributo', async () => {
    const { run } = setup();
    run.mockResolvedValueOnce('Senha incorreta');
    type('Senha-secreta-777');

    submit();
    await settleDialog();

    expect(document.body.innerHTML).not.toContain('Senha-secreta-777');
  });
});
