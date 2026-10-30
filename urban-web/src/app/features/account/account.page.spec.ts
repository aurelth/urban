import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { SessionStore } from '../../core/auth/session.store';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { AccountPage } from './account.page';

interface Harness {
  fixture: ComponentFixture<AccountPage>;
  http: HttpTestingController;
  html: HTMLElement;
  store: InstanceType<typeof SessionStore>;
  navigate: jest.SpiedFunction<Router['navigate']>;
}

async function setup(): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  const store = TestBed.inject(SessionStore);
  await signInAs(store, http, REGISTERED_MANAGER);
  const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  const fixture = TestBed.createComponent(AccountPage);
  fixture.detectChanges();
  await fixture.whenStable();
  return { fixture, http, html: fixture.nativeElement, store, navigate };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function testId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function type(root: ParentNode, selector: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function submitOf(root: ParentNode): void {
  root.querySelector('form')!.dispatchEvent(new Event('submit'));
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('AccountPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('tem o titulo e as duas secoes, e mostra o e-mail atual', async () => {
    const { html } = await setup();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Minha conta');
    expect(testId(html, 'email-section')?.querySelector('h2')?.textContent?.trim()).toBe('E-mail');
    expect(testId(html, 'password-section')?.querySelector('h2')?.textContent?.trim()).toBe(
      'Senha',
    );
    expect(testId(html, 'current-email')?.textContent?.trim()).toBe('sindica@example.com');
  });

  describe('trocar a senha', () => {
    function password(html: HTMLElement): HTMLElement {
      return testId(html, 'password-section')!;
    }

    function fillPasswords(
      html: HTMLElement,
      values: { current?: string; next?: string; confirm?: string },
    ): void {
      type(
        password(html),
        '[formControlName="currentPassword"]',
        values.current ?? 'Senha-antiga-123',
      );
      type(password(html), '[formControlName="newPassword"]', values.next ?? 'Senha-nova-456');
      type(
        password(html),
        '[formControlName="confirmPassword"]',
        values.confirm ?? values.next ?? 'Senha-nova-456',
      );
    }

    async function submitPasswords(h: Harness): Promise<void> {
      submitOf(password(h.html));
      await settle(h.fixture);
    }

    function errors(html: HTMLElement): string[] {
      return [...password(html).querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
    }

    it('pede a senha atual, a nova e a confirmacao, com o autocomplete certo', async () => {
      const { html } = await setup();

      const labels = [...password(html).querySelectorAll('mat-label')].map((l) =>
        l.textContent!.trim(),
      );
      expect(labels).toEqual(['Senha atual', 'Nova senha', 'Confirmar nova senha']);
      expect(
        password(html)
          .querySelector('[formControlName="currentPassword"]')!
          .getAttribute('autocomplete'),
      ).toBe('current-password');
      expect(
        password(html)
          .querySelector('[formControlName="newPassword"]')!
          .getAttribute('autocomplete'),
      ).toBe('new-password');
      expect(
        password(html)
          .querySelector('[formControlName="confirmPassword"]')!
          .getAttribute('autocomplete'),
      ).toBe('new-password');
      expect(password(html).textContent).toContain('Mínimo de 10 caracteres, com letra e número');
    });

    it('nao envia e mostra o que falta quando os campos estao vazios', async () => {
      const h = await setup();

      await submitPasswords(h);

      h.http.expectNone('/api/auth/change-password');
      expect(errors(h.html)).toEqual(['Informe a senha', 'Informe a senha', 'Informe a senha']);
    });

    it('aplica a politica de senha na senha nova', async () => {
      const h = await setup();
      fillPasswords(h.html, { next: 'curta1' });

      await submitPasswords(h);

      h.http.expectNone('/api/auth/change-password');
      expect(errors(h.html)).toContain('A senha precisa ter pelo menos 10 caracteres');
    });

    it('avisa quando a confirmacao nao e igual a senha nova', async () => {
      const h = await setup();
      fillPasswords(h.html, { next: 'Senha-nova-456', confirm: 'Senha-nova-457' });

      await submitPasswords(h);

      h.http.expectNone('/api/auth/change-password');
      expect(errors(h.html)).toContain('As senhas não são iguais');
    });

    it('envia so a senha atual e a nova, nunca a confirmacao', async () => {
      const h = await setup();
      fillPasswords(h.html, {});

      await submitPasswords(h);
      const request = h.http.expectOne('/api/auth/change-password');

      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({
        currentPassword: 'Senha-antiga-123',
        newPassword: 'Senha-nova-456',
      });
    });

    it('desabilita o botao e troca o texto enquanto envia', async () => {
      const h = await setup();
      fillPasswords(h.html, {});

      await submitPasswords(h);

      const button = password(h.html).querySelector<HTMLButtonElement>('button[type="submit"]')!;
      expect(button.disabled).toBe(true);
      expect(button.textContent?.trim()).toBe('Alterando');
      h.http.expectOne('/api/auth/change-password');
    });

    it('com sucesso encerra a sessao deste navegador e leva a entrada com o aviso', async () => {
      const h = await setup();
      fillPasswords(h.html, {});
      await submitPasswords(h);

      h.http
        .expectOne('/api/auth/change-password')
        .flush(null, { status: 204, statusText: 'No Content' });
      await settle(h.fixture);

      expect(h.store.status()).toBe('anonymous');
      expect(h.store.expired()).toBe(false);
      expect(h.navigate).toHaveBeenCalledWith(['/entrar'], {
        queryParams: { motivo: 'senha-alterada' },
      });
      h.http.expectNone('/api/auth/logout');
    });

    it('senha atual incorreta aparece no formulario, limpa o campo e nao encerra a sessao', async () => {
      const h = await setup();
      fillPasswords(h.html, { current: 'Senha-errada-999' });
      await submitPasswords(h);
      const [body, options] = failure(403, 'identity.password.incorrect');

      h.http.expectOne('/api/auth/change-password').flush(body, options);
      await settle(h.fixture);

      expect(password(h.html).querySelector('[role="alert"]')?.textContent?.trim()).toBe(
        'Senha atual incorreta',
      );
      expect(
        password(h.html).querySelector<HTMLInputElement>('[formControlName="currentPassword"]')!
          .value,
      ).toBe('');
      expect(h.store.status()).toBe('authenticated');
      expect(h.navigate).not.toHaveBeenCalled();
    });

    it.each([
      ['identity.password.unchanged', 'A nova senha precisa ser diferente da atual'],
      ['identity.password.too_short', 'A senha precisa ter pelo menos 10 caracteres'],
      [
        'identity.password.needs_letter_and_digit',
        'A senha precisa ter pelo menos uma letra e um número',
      ],
      [
        'identity.login.locked_out',
        'Conta bloqueada por muitas tentativas. Tente novamente em 15 minutos',
      ],
    ])('mostra o texto da interface para %s', async (code, message) => {
      const h = await setup();
      fillPasswords(h.html, {});
      await submitPasswords(h);
      const status = code === 'identity.login.locked_out' ? 423 : 400;
      const [body, options] = failure(status, code);

      h.http.expectOne('/api/auth/change-password').flush(body, options);
      await settle(h.fixture);

      expect(password(h.html).querySelector('[role="alert"]')?.textContent?.trim()).toBe(message);
      expect(h.store.status()).toBe('authenticated');
    });

    it('as senhas digitadas nunca aparecem no texto da pagina', async () => {
      const h = await setup();
      fillPasswords(h.html, {});
      await submitPasswords(h);
      const [body, options] = failure(403, 'identity.password.incorrect');

      h.http.expectOne('/api/auth/change-password').flush(body, options);
      await settle(h.fixture);

      expect(h.html.innerHTML).not.toContain('Senha-antiga-123');
      expect(h.html.innerHTML).not.toContain('Senha-nova-456');
    });
  });

  describe('trocar o e-mail', () => {
    function dialog(): HTMLElement | null {
      return document.querySelector('urban-change-email-dialog');
    }

    async function openDialog(h: Harness): Promise<void> {
      testId(h.html, 'change-email')!.click();
      await settle(h.fixture);
    }

    async function fillDialog(
      h: Harness,
      email = 'nova@example.com',
      pass = 'Senha-segura-123',
    ): Promise<void> {
      type(dialog()!, 'input[type="email"]', email);
      type(dialog()!, 'input[autocomplete="current-password"]', pass);
      submitOf(dialog()!);
      await settle(h.fixture);
    }

    function dialogAlert(): string {
      return dialog()?.querySelector('[role="alert"]')?.textContent?.trim() ?? '';
    }

    it('o botao e preenchido, como os outros botoes principais, e nao so contornado', async () => {
      const h = await setup();
      const button = testId(h.html, 'change-email')!;

      expect(button.classList).toContain('mat-mdc-unelevated-button');
      expect(button.classList).not.toContain('mat-mdc-outlined-button');
    });

    it('o botao abre o dialogo com o novo e-mail e a senha', async () => {
      const h = await setup();

      await openDialog(h);

      expect(testId(h.html, 'change-email')?.textContent?.trim()).toBe('Alterar e-mail');
      expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Alterar e-mail');
      expect(dialog()?.querySelector('input[type="email"]')).not.toBeNull();
      expect(dialog()?.querySelector('input[autocomplete="current-password"]')).not.toBeNull();
      expect(dialog()?.querySelector('button[type="submit"]')?.textContent?.trim()).toBe(
        'Enviar link de confirmação',
      );
    });

    it('nao envia e mostra o que falta quando os campos estao vazios', async () => {
      const h = await setup();
      await openDialog(h);

      submitOf(dialog()!);
      await settle(h.fixture);

      h.http.expectNone('/api/auth/change-email');
      const messages = [...dialog()!.querySelectorAll('mat-error')].map((e) =>
        e.textContent!.trim(),
      );
      expect(messages).toEqual(['Informe o e-mail', 'Informe a senha']);
    });

    it('recusa um endereco que nao parece e-mail', async () => {
      const h = await setup();
      await openDialog(h);

      await fillDialog(h, 'isto-nao-e-email');

      h.http.expectNone('/api/auth/change-email');
      expect(dialog()?.querySelector('mat-error')?.textContent?.trim()).toBe(
        'Informe um e-mail válido',
      );
    });

    it('envia a senha e o novo e-mail', async () => {
      const h = await setup();
      await openDialog(h);

      await fillDialog(h);
      const request = h.http.expectOne('/api/auth/change-email');

      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({
        password: 'Senha-segura-123',
        newEmail: 'nova@example.com',
      });
    });

    it('com sucesso fecha o dialogo e avisa em verde, sem prometer que o link saiu', async () => {
      const h = await setup();
      await openDialog(h);
      await fillDialog(h);

      h.http
        .expectOne('/api/auth/change-email')
        .flush(null, { status: 202, statusText: 'Accepted' });
      await settle(h.fixture);

      expect(dialog()).toBeNull();
      const notice = testId(h.html, 'email-requested');
      expect(notice?.getAttribute('role')).toBe('status');
      expect(notice?.textContent).toContain('nova@example.com');
      expect(notice?.textContent).toContain('Se o endereço puder ser usado');
      expect(notice?.textContent).toContain('o e-mail atual continua valendo');
      expect(notice?.querySelector('.text-success mat-icon')).not.toBeNull();
      expect(testId(h.html, 'current-email')?.textContent?.trim()).toBe('sindica@example.com');
    });

    it('nao encerra a sessao nem muda o e-mail mostrado ao pedir a troca', async () => {
      const h = await setup();
      await openDialog(h);
      await fillDialog(h);

      h.http
        .expectOne('/api/auth/change-email')
        .flush(null, { status: 202, statusText: 'Accepted' });
      await settle(h.fixture);

      expect(h.store.status()).toBe('authenticated');
      h.http.expectNone('/api/auth/logout');
    });

    it.each([
      [403, 'identity.password.incorrect', 'Senha incorreta'],
      [400, 'identity.email.invalid', 'Informe um e-mail válido'],
      [400, 'identity.email.unchanged', 'Este já é o e-mail da sua conta'],
      [
        423,
        'identity.login.locked_out',
        'Conta bloqueada por muitas tentativas. Tente novamente em 15 minutos',
      ],
    ])('mostra no dialogo o erro %i %s', async (status, code, message) => {
      const h = await setup();
      await openDialog(h);
      await fillDialog(h);
      const [body, options] = failure(status, code);

      h.http.expectOne('/api/auth/change-email').flush(body, options);
      await settle(h.fixture);

      expect(dialog()).not.toBeNull();
      expect(dialogAlert()).toBe(message);
      expect(h.store.status()).toBe('authenticated');
      expect(testId(h.html, 'email-requested')).toBeNull();
    });

    it('depois de um erro de senha, limpa o campo da senha e deixa tentar de novo', async () => {
      const h = await setup();
      await openDialog(h);
      await fillDialog(h, 'nova@example.com', 'Senha-errada-999');
      const [body, options] = failure(403, 'identity.password.incorrect');
      h.http.expectOne('/api/auth/change-email').flush(body, options);
      await settle(h.fixture);

      expect(
        dialog()!.querySelector<HTMLInputElement>('input[autocomplete="current-password"]')!.value,
      ).toBe('');
      type(dialog()!, 'input[autocomplete="current-password"]', 'Senha-segura-123');
      submitOf(dialog()!);
      await settle(h.fixture);

      expect(h.http.expectOne('/api/auth/change-email').request.body.password).toBe(
        'Senha-segura-123',
      );
    });

    it('cancelar fecha o dialogo sem chamar a API', async () => {
      const h = await setup();
      await openDialog(h);

      dialog()!.querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
      await settle(h.fixture);

      expect(dialog()).toBeNull();
      h.http.expectNone('/api/auth/change-email');
    });

    it('a senha digitada nunca aparece no texto da pagina', async () => {
      const h = await setup();
      await openDialog(h);
      await fillDialog(h, 'nova@example.com', 'Senha-secreta-777');
      h.http
        .expectOne('/api/auth/change-email')
        .flush(null, { status: 202, statusText: 'Accepted' });
      await settle(h.fixture);

      expect(document.body.innerHTML).not.toContain('Senha-secreta-777');
    });
  });
});
