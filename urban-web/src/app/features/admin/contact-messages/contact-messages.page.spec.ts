import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { ContactMessage } from '../../../core/admin/contact-messages.models';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import { configureDialogTest } from '../../../testing/dialog.harness';
import {
  CONTACT_MESSAGES_URL,
  READ_MESSAGE,
  contactMessage,
  contactMessagesPage,
} from '../../../testing/contact.fixtures';
import { chooseMenuOption, menuSelectOptions, menuSelectValue } from '../../../testing/menu-select';
import { ContactMessagesPage } from './contact-messages.page';

const NO_CONTENT = { status: 204, statusText: 'No Content' };
const UNREAD = contactMessage();

function setup(): {
  fixture: ComponentFixture<ContactMessagesPage>;
  http: HttpTestingController;
  html: HTMLElement;
} {
  configureDialogTest();
  const fixture = TestBed.createComponent(ContactMessagesPage);
  return { fixture, http: TestBed.inject(HttpTestingController), html: fixture.nativeElement };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((r) => r.url === CONTACT_MESSAGES_URL && r.method === 'GET');
}

function params(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

async function open(items: ContactMessage[] = [UNREAD, READ_MESSAGE], total = items.length) {
  const harness = setup();
  harness.fixture.detectChanges();
  listRequest(harness.http).flush(contactMessagesPage(items, total));
  await settle(harness.fixture);
  return harness;
}

function testId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(root: ParentNode, id: string): string {
  return testId(root, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function cards(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="message"]')];
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ContactMessagesPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra o título e avisa que está carregando', () => {
      const { fixture, http, html } = setup();

      fixture.detectChanges();

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Mensagens');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(text(html, 'loading')).toContain('Carregando as mensagens');
      expect(params(listRequest(http)).toString()).toBe('page=1&pageSize=20');
    });

    it('mostra o erro e deixa tentar de novo quando a busca falha', async () => {
      const { fixture, http, html } = setup();
      fixture.detectChanges();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(text(html, 'load-error')).toContain('Não foi possível carregar as mensagens');

      testId(html, 'retry')!.click();
      listRequest(http).flush(contactMessagesPage([UNREAD]));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(cards(html)).toHaveLength(1);
    });

    it('sem nenhuma mensagem, avisa que ainda não chegou nada', async () => {
      const { html } = await open([], 0);

      expect(text(html, 'empty')).toContain('Nenhuma mensagem recebida ainda');
      expect(cards(html)).toHaveLength(0);
    });
  });

  describe('lista', () => {
    it('mostra um cartão por mensagem, com remetente, condomínio, data, assunto e texto', async () => {
      const { html } = await open();

      const [unread] = cards(html);
      expect(cards(html)).toHaveLength(2);
      expect(text(unread, 'message-subject')).toBe('Corrigir o CNPJ do condomínio');
      expect(text(unread, 'message-sender')).toBe('Ana');
      expect(text(unread, 'message-email')).toBe('ana@example.com');
      expect(text(unread, 'message-condominium')).toBe('Residencial Aurora');
      expect(text(unread, 'message-date')).toBe('Enviada em 13/11/2026 às 12:05');
      expect(testId(unread, 'message-body')!.textContent).toContain(
        'Digitei o CNPJ errado no cadastro.\nPode corrigir',
      );
    });

    it('sem nome, o e-mail é o remetente e aparece uma vez só; sem condomínio, avisa', async () => {
      const { html } = await open();

      const [, read] = cards(html);
      expect(text(read, 'message-sender')).toBe('carlos@example.com');
      expect(testId(read, 'message-email')).toBeNull();
      expect(text(read, 'message-condominium')).toBe('Ainda sem condomínio cadastrado');
    });

    it('mostra a situação em texto e ícone: não lida, ou lida com o dia e a hora', async () => {
      const { html } = await open();

      const [unread, read] = cards(html);
      expect(text(unread, 'message-status')).toBe('Não lida');
      expect(text(read, 'message-status')).toBe('Lida em 12/11/2026 às 17:00');
      for (const card of [unread, read]) {
        expect(testId(card, 'message-status')!.querySelector('mat-icon')).not.toBeNull();
      }
    });

    it('conta as mensagens no plural e no singular', async () => {
      const many = await open([UNREAD], 2);
      expect(text(many.html, 'total')).toBe('2 mensagens');
      TestBed.resetTestingModule();

      const one = await open([UNREAD], 1);
      expect(text(one.html, 'total')).toBe('1 mensagem');
    });

    it('oferece responder por e-mail ao remetente, com o assunto', async () => {
      const { html } = await open();

      const link = testId(cards(html)[0], 'reply') as HTMLAnchorElement;
      expect(link.textContent?.trim()).toBe('Responder por e-mail');
      expect(link.getAttribute('href')).toBe(
        'mailto:ana@example.com?subject=Re%3A%20Corrigir%20o%20CNPJ%20do%20condom%C3%ADnio',
      );
    });
  });

  describe('marcar como lida', () => {
    it('só as não lidas têm o botão', async () => {
      const { html } = await open();

      const [unread, read] = cards(html);
      expect(testId(unread, 'mark-read')).not.toBeNull();
      expect(testId(read, 'mark-read')).toBeNull();
    });

    it('marca, busca a lista de novo e avisa', async () => {
      const { fixture, http, html } = await open();

      testId(cards(html)[0], 'mark-read')!.click();
      http.expectOne(`${CONTACT_MESSAGES_URL}/${UNREAD.id}/read`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(
        contactMessagesPage([{ ...UNREAD, readAt: '2026-11-13T16:00:00+00:00' }, READ_MESSAGE]),
      );
      await settle(fixture);

      expect(text(cards(html)[0], 'message-status')).toBe('Lida em 13/11/2026 às 13:00');
      expect(testId(cards(html)[0], 'mark-read')).toBeNull();
      expect(testId(html, 'notice')?.getAttribute('role')).toBe('status');
      expect(text(html, 'notice')).toBe('Mensagem marcada como lida');
    });

    it('trava o botão enquanto a chamada está em andamento', async () => {
      const { fixture, http, html } = await open();

      testId(cards(html)[0], 'mark-read')!.click();
      fixture.detectChanges();

      expect((testId(cards(html)[0], 'mark-read') as HTMLButtonElement).disabled).toBe(true);
      http.expectOne(`${CONTACT_MESSAGES_URL}/${UNREAD.id}/read`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(contactMessagesPage([UNREAD, READ_MESSAGE]));
    });

    it.each([
      [
        404,
        'identity.contact.message_not_found',
        'Esta mensagem não existe mais. A lista foi atualizada',
      ],
      [403, 'identity.contact.read_forbidden', 'Só o administrador pode ler as mensagens'],
      [500, 'identity.internal', 'Algo deu errado. Tente novamente em instantes'],
    ])('explica a recusa %s sem apagar a lista', async (status, code, message) => {
      const { fixture, http, html } = await open();

      testId(cards(html)[0], 'mark-read')!.click();
      http.expectOne(`${CONTACT_MESSAGES_URL}/${UNREAD.id}/read`).flush(...failure(status, code));
      await flushPromises();
      if (status === 404) {
        listRequest(http).flush(contactMessagesPage([READ_MESSAGE]));
      }
      await settle(fixture);

      expect(testId(html, 'action-error')?.getAttribute('role')).toBe('alert');
      expect(text(html, 'action-error')).toBe(message);
      expect(testId(html, 'notice')).toBeNull();
      expect(cards(html).length).toBeGreaterThan(0);
    });
  });

  describe('filtro', () => {
    it('oferece todas ou só as não lidas', async () => {
      const { html } = await open();

      const filter = testId(html, 'filter-status')!;
      expect(menuSelectOptions(filter).map((o) => [o.value, o.label])).toEqual([
        ['', 'Todas'],
        ['unread', 'Não lidas'],
      ]);
      expect(menuSelectValue(filter)).toBe('');
      expect(testId(html, 'clear-filters')).toBeNull();
    });

    it('escolher "Não lidas" busca de novo a partir da página 1', async () => {
      const { http, html, fixture } = await open([UNREAD], 60);

      chooseMenuOption(testId(html, 'filter-status')!, 'unread');
      const request = listRequest(http);
      request.flush(contactMessagesPage([UNREAD], 1));
      await settle(fixture);

      expect(params(request).get('status')).toBe('unread');
      expect(params(request).get('page')).toBe('1');
    });

    it('sem resultado com filtro, avisa e oferece limpar o filtro', async () => {
      const { http, html, fixture } = await open();

      chooseMenuOption(testId(html, 'filter-status')!, 'unread');
      listRequest(http).flush(contactMessagesPage([], 0));
      await settle(fixture);

      expect(text(html, 'no-results')).toContain('Nenhuma mensagem não lida');
      expect(testId(html, 'empty')).toBeNull();

      testId(html, 'clear-filters')!.click();
      const request = listRequest(http);
      request.flush(contactMessagesPage([UNREAD, READ_MESSAGE]));
      await settle(fixture);

      expect(params(request).toString()).toBe('page=1&pageSize=20');
      expect(menuSelectValue(testId(html, 'filter-status')!)).toBe('');
      expect(testId(html, 'clear-filters')).toBeNull();
    });
  });

  describe('paginação', () => {
    it('some quando cabe tudo numa página', async () => {
      const { html } = await open();

      expect(testId(html, 'pagination')).toBeNull();
    });

    it('mostra a página atual e navega para a próxima e para a anterior', async () => {
      const { http, html, fixture } = await open([UNREAD], 45);

      expect(text(html, 'page-status')).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      testId(html, 'next')!.click();
      const next = listRequest(http);
      next.flush(contactMessagesPage([READ_MESSAGE], 45, 2));
      await settle(fixture);
      expect(params(next).get('page')).toBe('2');
      expect(text(html, 'page-status')).toBe('Página 2 de 3');

      testId(html, 'previous')!.click();
      const previous = listRequest(http);
      previous.flush(contactMessagesPage([UNREAD], 45, 1));
      await settle(fixture);
      expect(params(previous).get('page')).toBe('1');
    });
  });
});
