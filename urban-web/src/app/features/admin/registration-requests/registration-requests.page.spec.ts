import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import { setupComponent, settle } from '../../../testing/component.harness';
import {
  REGISTRATION_REQUESTS_URL,
  SECOND_REQUEST,
  registrationRequest,
} from '../../../testing/registration-requests.fixtures';
import { RegistrationRequestsPage } from './registration-requests.page';

const FIRST = registrationRequest();
const NO_CONTENT = { status: 204, statusText: 'No Content' };

function byTestId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

/** O texto como a pessoa lê: o Angular mantém as quebras de linha e os recuos da marcação. */
function textOf(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function items(html: HTMLElement): HTMLElement[] {
  return Array.from(html.querySelectorAll<HTMLElement>('[data-testid="request"]'));
}

async function open(list = [FIRST, SECOND_REQUEST]) {
  const harness = setupComponent(RegistrationRequestsPage);
  harness.fixture.detectChanges();
  harness.http.expectOne(REGISTRATION_REQUESTS_URL).flush(list);
  await settle(harness.fixture);
  return harness;
}

/** Responde a decisao e a lista que a tela busca em seguida. */
async function answer(
  harness: Awaited<ReturnType<typeof open>>,
  action: 'approve' | 'reject',
  target: { id: string },
  after: unknown[],
  failure?: { status: number; code: string },
): Promise<void> {
  const request = harness.http.expectOne(`${REGISTRATION_REQUESTS_URL}/${target.id}/${action}`);
  if (failure === undefined) {
    request.flush(null, NO_CONTENT);
  } else {
    request.flush(problem(failure.status, failure.code), {
      status: failure.status,
      statusText: 'Erro',
    });
  }
  await flushPromises();
  if (failure === undefined || failure.status === 404 || failure.status === 409) {
    harness.http.expectOne(REGISTRATION_REQUESTS_URL).flush(after);
  }
  await settle(harness.fixture);
}

describe('RegistrationRequestsPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o titulo e avisa que esta carregando', () => {
    const { fixture, http, html } = setupComponent(RegistrationRequestsPage);

    fixture.detectChanges();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Pedidos de cadastro');
    expect(textOf(byTestId(html, 'loading'))).toContain('Carregando os pedidos');
    http.expectOne(REGISTRATION_REQUESTS_URL);
  });

  it('lista cada pedido com nome, e-mail, condominio e quando chegou', async () => {
    const { html } = await open();

    const [first, second] = items(html);
    expect(items(html)).toHaveLength(2);
    expect(byTestId(first, 'request-name')?.textContent?.trim()).toBe('Marina');
    expect(byTestId(first, 'request-email')?.textContent?.trim()).toBe('marina@example.com');
    expect(byTestId(first, 'request-condominium')?.textContent?.trim()).toBe('Residencial Aurora');
    expect(textOf(byTestId(first, 'request-date'))).toBe('Recebido em 13/11/2026 às 01:23');
    expect(byTestId(second, 'request-name')?.textContent?.trim()).toBe('Carlos');
    expect(byTestId(first, 'approve')?.textContent?.trim()).toBe('Aprovar');
    expect(byTestId(first, 'reject')?.textContent?.trim()).toBe('Recusar');
  });

  it('mostra o estado vazio quando nao ha pedidos', async () => {
    const { html } = await open([]);

    expect(items(html)).toHaveLength(0);
    expect(textOf(byTestId(html, 'empty'))).toContain('Nenhum pedido aguardando análise');
  });

  it('avisa o erro da busca com um botao para tentar de novo', async () => {
    const { fixture, http, html } = setupComponent(RegistrationRequestsPage);
    fixture.detectChanges();
    http.expectOne(REGISTRATION_REQUESTS_URL).flush(null, { status: 500, statusText: 'Erro' });
    await settle(fixture);

    expect(textOf(byTestId(html, 'load-error'))).toContain('Não foi possível carregar os pedidos');

    byTestId(html, 'retry')!.click();
    await settle(fixture);
    http.expectOne(REGISTRATION_REQUESTS_URL).flush([FIRST]);
    await settle(fixture);

    expect(items(html)).toHaveLength(1);
  });

  describe('aprovar', () => {
    it('aprova sem pedir confirmacao, tira o pedido da lista e avisa que o link foi enviado', async () => {
      const harness = await open();

      byTestId(items(harness.html)[0], 'approve')!.click();
      await answer(harness, 'approve', FIRST, [SECOND_REQUEST]);

      expect(items(harness.html)).toHaveLength(1);
      expect(textOf(byTestId(harness.html, 'notice'))).toContain(
        'Pedido de Marina aprovado. Enviamos o link de confirmação para marina@example.com',
      );
      expect(byTestId(harness.html, 'action-error')).toBeNull();
    });

    it('trava os botoes de todos os pedidos enquanto a decisao esta a caminho', async () => {
      const harness = await open();

      byTestId(items(harness.html)[0], 'approve')!.click();
      harness.fixture.detectChanges();

      const buttons = Array.from(
        harness.html.querySelectorAll<HTMLButtonElement>(
          '[data-testid="approve"], [data-testid="reject"]',
        ),
      );
      expect(buttons).toHaveLength(4);
      expect(buttons.every((button) => button.disabled)).toBe(true);
      await answer(harness, 'approve', FIRST, [SECOND_REQUEST]);
    });
  });

  describe('recusar', () => {
    it('pede confirmacao antes de recusar e explica que a recusa e final', async () => {
      const harness = await open();

      byTestId(items(harness.html)[0], 'reject')!.click();
      harness.fixture.detectChanges();

      const confirmation = byTestId(items(harness.html)[0], 'reject-confirmation');
      expect(textOf(confirmation)).toContain('Recusar o pedido de Marina?');
      expect(textOf(confirmation)).toContain(
        'A recusa é final: nada é enviado à pessoa e novos pedidos com este e-mail serão ignorados',
      );
      expect(byTestId(confirmation!, 'confirm-reject')?.textContent?.trim()).toBe('Recusar pedido');
      expect(byTestId(confirmation!, 'cancel-reject')?.textContent?.trim()).toBe('Cancelar');
      harness.http.expectNone(`${REGISTRATION_REQUESTS_URL}/${FIRST.id}/reject`);
    });

    it('desiste sem chamar a API', async () => {
      const harness = await open();
      byTestId(items(harness.html)[0], 'reject')!.click();
      harness.fixture.detectChanges();

      byTestId(items(harness.html)[0], 'cancel-reject')!.click();
      harness.fixture.detectChanges();

      expect(byTestId(harness.html, 'reject-confirmation')).toBeNull();
      expect(items(harness.html)).toHaveLength(2);
      harness.http.expectNone(`${REGISTRATION_REQUESTS_URL}/${FIRST.id}/reject`);
    });

    it('deixa a confirmacao aberta em um pedido so', async () => {
      const harness = await open();

      byTestId(items(harness.html)[0], 'reject')!.click();
      harness.fixture.detectChanges();
      byTestId(items(harness.html)[1], 'reject')!.click();
      harness.fixture.detectChanges();

      expect(harness.html.querySelectorAll('[data-testid="reject-confirmation"]')).toHaveLength(1);
      expect(byTestId(items(harness.html)[1], 'reject-confirmation')).not.toBeNull();
    });

    it('recusa depois da confirmacao, tira o pedido da lista e avisa que nada foi enviado', async () => {
      const harness = await open();
      byTestId(items(harness.html)[0], 'reject')!.click();
      harness.fixture.detectChanges();

      byTestId(items(harness.html)[0], 'confirm-reject')!.click();
      await answer(harness, 'reject', FIRST, [SECOND_REQUEST]);

      expect(items(harness.html)).toHaveLength(1);
      expect(byTestId(harness.html, 'reject-confirmation')).toBeNull();
      expect(textOf(byTestId(harness.html, 'notice'))).toContain(
        'Pedido de Marina recusado. Nada foi enviado à pessoa',
      );
    });
  });

  describe.each([
    ['approve', 'approve'],
    ['reject', 'confirm-reject'],
  ] as const)('quando a API recusa a decisao (%s)', (action, button) => {
    async function decide(
      harness: Awaited<ReturnType<typeof open>>,
      failure: { status: number; code: string },
      after: unknown[],
    ): Promise<void> {
      if (action === 'reject') {
        byTestId(items(harness.html)[0], 'reject')!.click();
        harness.fixture.detectChanges();
      }
      byTestId(items(harness.html)[0], button)!.click();
      await answer(harness, action, FIRST, after, failure);
    }

    it.each([
      [
        404,
        'identity.registration_request.not_found',
        'Este pedido não existe mais. A lista foi atualizada',
      ],
      [
        409,
        'identity.user.invalid_transition',
        'Este pedido já foi decidido. A lista foi atualizada',
      ],
    ])('atualiza a lista e explica o %i', async (status, code, message) => {
      const harness = await open();

      await decide(harness, { status, code }, [SECOND_REQUEST]);

      expect(items(harness.html)).toHaveLength(1);
      expect(textOf(byTestId(harness.html, 'action-error'))).toContain(message);
      expect(byTestId(harness.html, 'notice')).toBeNull();
    });

    it('explica a falta de permissao sem mexer na lista', async () => {
      const harness = await open();

      await decide(harness, { status: 403, code: 'identity.registration_request.forbidden' }, []);

      expect(items(harness.html)).toHaveLength(2);
      expect(textOf(byTestId(harness.html, 'action-error'))).toContain(
        'Só o administrador pode decidir pedidos de cadastro',
      );
    });

    it('mostra o aviso geral para qualquer outra falha', async () => {
      const harness = await open();

      await decide(harness, { status: 500, code: 'x' }, []);

      expect(textOf(byTestId(harness.html, 'action-error'))).toContain(
        'Algo deu errado. Tente novamente em instantes',
      );
    });
  });

  it('troca o aviso de sucesso pelo de erro, e o contrario, na decisao seguinte', async () => {
    const harness = await open();
    byTestId(items(harness.html)[0], 'approve')!.click();
    await answer(harness, 'approve', FIRST, [SECOND_REQUEST]);
    expect(byTestId(harness.html, 'notice')).not.toBeNull();

    byTestId(items(harness.html)[0], 'approve')!.click();
    await answer(harness, 'approve', SECOND_REQUEST, [], { status: 500, code: 'x' });

    expect(byTestId(harness.html, 'notice')).toBeNull();
    expect(byTestId(harness.html, 'action-error')).not.toBeNull();
  });
});
