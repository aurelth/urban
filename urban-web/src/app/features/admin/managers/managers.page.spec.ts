import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { Manager } from '../../../core/admin/admin.models';
import {
  MANAGERS_URL,
  SUSPENDED_MANAGER,
  manager,
  managersPage,
} from '../../../testing/admin.fixtures';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import { setupComponent } from '../../../testing/component.harness';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  removeOverlays,
  submitDialog,
  typeIn,
} from '../../../testing/dialog.harness';
import { chooseMenuOption, menuSelectOptions, menuSelectValue } from '../../../testing/menu-select';
import { ManagersPage } from './managers.page';

const NO_CONTENT = { status: 204, statusText: 'No Content' };
const TAG = 'urban-suspend-manager-dialog';

const ANA = manager();
const PENDING = manager({
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5003',
  email: 'paula@example.com',
  name: null,
  condominiumId: null,
  condominiumName: null,
  status: 'PendingVerification',
});
const DEACTIVATED = manager({
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5004',
  email: 'rui@example.com',
  name: 'Rui',
  status: 'Deactivated',
});

interface Options {
  items?: Manager[];
  total?: number;
}

function setup(): ReturnType<typeof setupComponent<ManagersPage>> {
  configureDialogTest();
  const fixture = TestBed.createComponent(ManagersPage);
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
  return http.expectOne((request) => request.url === MANAGERS_URL && request.method === 'GET');
}

function params(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

async function open(options: Options = {}) {
  const harness = setup();
  harness.fixture.detectChanges();
  const items = options.items ?? [ANA, SUSPENDED_MANAGER];
  listRequest(harness.http).flush(managersPage(items, options.total ?? items.length));
  await settle(harness.fixture);
  return harness;
}

function testId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(root: ParentNode, id: string): string {
  return testId(root, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="manager-row"]')];
}

function button(root: ParentNode, label: string): HTMLButtonElement | null {
  return root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
}

function choose(html: HTMLElement, id: string, value: string): void {
  const field = testId(html, id)!;
  if (field.tagName.toLowerCase() === 'urban-menu-select') {
    chooseMenuOption(field, value);
    return;
  }
  (field as HTMLInputElement).value = value;
  field.dispatchEvent(new Event('change'));
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ManagersPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    removeOverlays();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra o titulo e avisa que esta carregando', () => {
      const { fixture, http, html } = setup();

      fixture.detectChanges();

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Síndicos');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(text(html, 'loading')).toContain('Carregando os síndicos');
      expect(testId(html, 'managers-table')).toBeNull();
      expect(params(listRequest(http)).toString()).toBe('page=1&pageSize=20');
    });

    it('mostra o erro e deixa tentar de novo quando a busca falha', async () => {
      const { fixture, http, html } = setup();
      fixture.detectChanges();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(text(html, 'load-error')).toContain('Não foi possível carregar os síndicos');

      testId(html, 'retry')!.click();
      listRequest(http).flush(managersPage([ANA]));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(1);
    });
  });

  describe('lista', () => {
    it('mostra uma linha por sindico, com nome, e-mail, condominio, situacao e cadastro', async () => {
      const { html } = await open();

      const [ana, carlos] = rows(html);
      expect(rows(html)).toHaveLength(2);
      expect(text(ana, 'manager-name')).toBe('Ana');
      expect(text(ana, 'manager-email')).toBe('ana@example.com');
      expect(text(ana, 'manager-condominium')).toBe('Residencial Aurora');
      expect(text(ana, 'manager-status')).toBe('Ativo');
      expect(text(ana, 'manager-created')).toBe('01/11/2026');
      expect(testId(ana, 'manager-reason')).toBeNull();
      expect(text(carlos, 'manager-name')).toBe('Carlos');
      expect(text(carlos, 'manager-condominium')).toBe('Edifício Bela Vista');
    });

    it('mostra o motivo da suspensao junto da situacao', async () => {
      const { html } = await open();

      const [, carlos] = rows(html);
      expect(text(carlos, 'manager-status')).toBe('Suspenso');
      expect(text(carlos, 'manager-reason')).toBe('Motivo: Uso indevido da plataforma');
    });

    it('mostra "—" onde falta nome ou condominio, e a situacao em texto e icone', async () => {
      const { html } = await open({ items: [PENDING, DEACTIVATED] });

      const [pending, deactivated] = rows(html);
      expect(text(pending, 'manager-name')).toBe('—');
      expect(text(pending, 'manager-condominium')).toBe('—');
      expect(text(pending, 'manager-status')).toBe('Aguardando confirmação');
      expect(text(deactivated, 'manager-status')).toBe('Desativado');
      for (const row of [pending, deactivated]) {
        expect(testId(row, 'manager-status')!.querySelector('mat-icon')).not.toBeNull();
      }
    });

    it('descreve a tabela para leitores de tela', async () => {
      const { html } = await open();

      expect(html.querySelector('table caption')?.textContent?.trim()).toBe('Lista de síndicos');
      const headers = [...html.querySelectorAll('th')].map((header) => header.textContent!.trim());
      expect(headers).toEqual(['Síndico', 'Condomínio', 'Situação', 'Cadastro', 'Ações']);
      expect(html.querySelectorAll('th[scope="col"]')).toHaveLength(5);
    });

    it('conta os sindicos no plural e no singular', async () => {
      const many = await open({ items: [ANA], total: 2 });
      expect(text(many.html, 'total')).toBe('2 síndicos');
      TestBed.resetTestingModule();

      const one = await open({ items: [ANA], total: 1 });
      expect(text(one.html, 'total')).toBe('1 síndico');
    });

    it('sem nenhum sindico, avisa que ainda nao ha cadastros', async () => {
      const { html } = await open({ items: [], total: 0 });

      expect(text(html, 'empty')).toContain('Nenhum síndico cadastrado ainda');
      expect(testId(html, 'managers-table')).toBeNull();
      expect(testId(html, 'no-results')).toBeNull();
    });
  });

  describe('acoes por situacao', () => {
    it('sindico ativo ou desativado pode ser suspenso, mas nao reativado', async () => {
      const { html } = await open({ items: [ANA, DEACTIVATED] });

      const [ana, rui] = rows(html);
      expect(button(ana, 'Suspender Ana')).not.toBeNull();
      expect(button(ana, 'Reativar Ana')).toBeNull();
      expect(button(rui, 'Suspender Rui')).not.toBeNull();
    });

    it('sindico suspenso so pode ser reativado', async () => {
      const { html } = await open();

      const [, carlos] = rows(html);
      expect(button(carlos, 'Reativar Carlos')).not.toBeNull();
      expect(button(carlos, 'Suspender Carlos')).toBeNull();
    });

    it('sindico que ainda nao confirmou o e-mail nao tem acao', async () => {
      const { html } = await open({ items: [PENDING] });

      expect(rows(html)[0].querySelectorAll('button')).toHaveLength(0);
    });

    it('usa o e-mail no nome do botao quando o sindico nao tem nome', async () => {
      const { html } = await open({ items: [manager({ name: null })] });

      expect(button(html, 'Suspender ana@example.com')).not.toBeNull();
    });
  });

  describe('filtros', () => {
    it('oferece situacao e busca', async () => {
      const { html } = await open();

      const status = testId(html, 'filter-status')!;
      expect(menuSelectOptions(status).map((option) => [option.value, option.label])).toEqual([
        ['', 'Todos'],
        ['active', 'Ativos'],
        ['suspended', 'Suspensos'],
        ['deactivated', 'Desativados'],
        ['pendingVerification', 'Aguardando confirmação'],
      ]);
      expect(menuSelectValue(status)).toBe('');
      expect(testId(html, 'filter-search')).not.toBeNull();
      expect(testId(html, 'clear-filters')).toBeNull();
    });

    it.each([
      ['filter-status', 'suspended', 'status', 'suspended'],
      ['filter-status', 'pendingVerification', 'status', 'pendingVerification'],
      ['filter-search', 'carlos', 'search', 'carlos'],
    ])('mexer em %s para "%s" busca de novo a partir da pagina 1', async (id, value, key, sent) => {
      const { http, html, fixture } = await open({ items: [ANA], total: 60 });

      choose(html, id, value);
      const request = listRequest(http);
      request.flush(managersPage([SUSPENDED_MANAGER], 1));
      await settle(fixture);

      expect(params(request).get(key)).toBe(sent);
      expect(params(request).get('page')).toBe('1');
    });

    it('sem resultado com filtro, avisa e oferece limpar os filtros', async () => {
      const { http, html, fixture } = await open();

      choose(html, 'filter-search', 'zzz');
      listRequest(http).flush(managersPage([], 0));
      await settle(fixture);

      expect(text(html, 'no-results')).toContain('Nenhum síndico encontrado com estes filtros');
      expect(testId(html, 'empty')).toBeNull();

      testId(html, 'clear-filters')!.click();
      const request = listRequest(http);
      request.flush(managersPage([ANA, SUSPENDED_MANAGER]));
      await settle(fixture);

      expect(params(request).toString()).toBe('page=1&pageSize=20');
      expect((testId(html, 'filter-search') as HTMLInputElement).value).toBe('');
      expect(menuSelectValue(testId(html, 'filter-status')!)).toBe('');
      expect(testId(html, 'clear-filters')).toBeNull();
    });
  });

  describe('paginacao', () => {
    it('some quando cabe tudo numa pagina', async () => {
      const { html } = await open();

      expect(testId(html, 'pagination')).toBeNull();
    });

    it('mostra a pagina atual e navega para a proxima e para a anterior', async () => {
      const { http, html, fixture } = await open({ items: [ANA], total: 45 });

      expect(text(html, 'page-status')).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      testId(html, 'next')!.click();
      const next = listRequest(http);
      next.flush(managersPage([SUSPENDED_MANAGER], 45, 2));
      await settle(fixture);
      expect(params(next).get('page')).toBe('2');
      expect(text(html, 'page-status')).toBe('Página 2 de 3');

      testId(html, 'previous')!.click();
      const previous = listRequest(http);
      previous.flush(managersPage([ANA], 45, 1));
      await settle(fixture);
      expect(params(previous).get('page')).toBe('1');
      expect(text(html, 'page-status')).toBe('Página 1 de 3');
    });

    it('na ultima pagina, o botao de proxima fica desabilitado', async () => {
      const { http, html, fixture } = await open({ items: [ANA], total: 25 });

      testId(html, 'next')!.click();
      listRequest(http).flush(managersPage([SUSPENDED_MANAGER], 25, 2));
      await settle(fixture);

      expect((testId(html, 'next') as HTMLButtonElement).disabled).toBe(true);
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(false);
    });
  });

  describe('suspender', () => {
    async function openSuspend(harness: Awaited<ReturnType<typeof open>>): Promise<void> {
      button(harness.html, 'Suspender Ana')!.click();
      TestBed.tick();
      await harness.fixture.whenStable();
    }

    it('abre o dialogo, grava o motivo, atualiza a lista e avisa', async () => {
      const harness = await open();

      await openSuspend(harness);
      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Suspender Ana');
      typeIn(TAG, 'reason', 'Uso indevido');
      submitDialog(TAG);
      await settle(harness.fixture);

      const post = harness.http.expectOne(`${MANAGERS_URL}/${ANA.id}/suspend`);
      expect(post.request.body).toEqual({ reason: 'Uso indevido' });
      post.flush(null, NO_CONTENT);
      await settle(harness.fixture);
      listRequest(harness.http).flush(
        managersPage([
          manager({ status: 'Suspended', statusReason: 'Uso indevido' }),
          SUSPENDED_MANAGER,
        ]),
      );
      await settle(harness.fixture);

      expect(document.querySelector(TAG)).toBeNull();
      expect(text(harness.html, 'notice')).toBe('Acesso de Ana suspenso');
      expect(text(rows(harness.html)[0], 'manager-status')).toBe('Suspenso');
    });

    it('cancelar nao chama a API nem avisa nada', async () => {
      const harness = await open();

      await openSuspend(harness);
      dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
      await settle(harness.fixture);

      harness.http.expectNone(`${MANAGERS_URL}/${ANA.id}/suspend`);
      expect(testId(harness.html, 'notice')).toBeNull();
    });

    it.each([
      [
        409,
        'identity.user.invalid_transition',
        'A situação deste síndico mudou. A lista foi atualizada',
      ],
      [404, 'identity.manager.not_found', 'Este síndico não existe mais. A lista foi atualizada'],
    ])(
      'recusa %s: explica no dialogo, que continua aberto, e atualiza a lista',
      async (status, code, message) => {
        const harness = await open();

        await openSuspend(harness);
        typeIn(TAG, 'reason', 'Motivo');
        submitDialog(TAG);
        await settle(harness.fixture);
        const [body, options] = failure(status, code);
        harness.http.expectOne(`${MANAGERS_URL}/${ANA.id}/suspend`).flush(body, options);
        await settle(harness.fixture);
        listRequest(harness.http).flush(managersPage([SUSPENDED_MANAGER]));
        await settle(harness.fixture);

        expect(alertOf(TAG)).toBe(message);
        expect(rows(harness.html)).toHaveLength(1);
        expect(testId(harness.html, 'notice')).toBeNull();
      },
    );

    it('recusa de permissao: explica no dialogo e nao busca a lista', async () => {
      const harness = await open();

      await openSuspend(harness);
      typeIn(TAG, 'reason', 'Motivo');
      submitDialog(TAG);
      await settle(harness.fixture);
      const [body, options] = failure(403, 'identity.administration.forbidden');
      harness.http.expectOne(`${MANAGERS_URL}/${ANA.id}/suspend`).flush(body, options);
      await settle(harness.fixture);

      expect(alertOf(TAG)).toBe('Só o administrador pode administrar os síndicos');
      harness.http.expectNone(MANAGERS_URL);
    });
  });

  describe('reativar', () => {
    it('reativa com um clique, sem pedir motivo, e avisa', async () => {
      const harness = await open();

      button(harness.html, 'Reativar Carlos')!.click();
      await settle(harness.fixture);
      expect(document.querySelector(TAG)).toBeNull();
      harness.http
        .expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`)
        .flush(null, NO_CONTENT);
      await settle(harness.fixture);
      listRequest(harness.http).flush(
        managersPage([
          ANA,
          manager({ ...SUSPENDED_MANAGER, status: 'Active', statusReason: null }),
        ]),
      );
      await settle(harness.fixture);

      expect(text(harness.html, 'notice')).toBe('Acesso de Carlos reativado');
      expect(text(rows(harness.html)[1], 'manager-status')).toBe('Ativo');
      expect(testId(harness.html, 'action-error')).toBeNull();
    });

    it('trava os botoes de todas as linhas enquanto a acao esta a caminho', async () => {
      const harness = await open();

      button(harness.html, 'Reativar Carlos')!.click();
      harness.fixture.detectChanges();

      expect(button(harness.html, 'Suspender Ana')!.disabled).toBe(true);
      expect(button(harness.html, 'Reativar Carlos')!.disabled).toBe(true);
      harness.http
        .expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`)
        .flush(null, NO_CONTENT);
      await settle(harness.fixture);
      listRequest(harness.http).flush(managersPage([ANA, SUSPENDED_MANAGER]));
      await settle(harness.fixture);
    });

    it('recusa 409: mostra o erro na pagina e atualiza a lista', async () => {
      const harness = await open();

      button(harness.html, 'Reativar Carlos')!.click();
      await settle(harness.fixture);
      const [body, options] = failure(409, 'identity.user.invalid_transition');
      harness.http
        .expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`)
        .flush(body, options);
      await settle(harness.fixture);
      listRequest(harness.http).flush(managersPage([ANA]));
      await settle(harness.fixture);

      expect(text(harness.html, 'action-error')).toContain(
        'A situação deste síndico mudou. A lista foi atualizada',
      );
      expect(testId(harness.html, 'notice')).toBeNull();
      expect(rows(harness.html)).toHaveLength(1);
    });

    it('uma nova acao apaga o aviso da anterior', async () => {
      const harness = await open();
      button(harness.html, 'Reativar Carlos')!.click();
      await settle(harness.fixture);
      harness.http
        .expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`)
        .flush(null, NO_CONTENT);
      await settle(harness.fixture);
      listRequest(harness.http).flush(managersPage([ANA, SUSPENDED_MANAGER]));
      await settle(harness.fixture);
      expect(testId(harness.html, 'notice')).not.toBeNull();

      button(harness.html, 'Reativar Carlos')!.click();
      harness.fixture.detectChanges();

      expect(testId(harness.html, 'notice')).toBeNull();
      harness.http
        .expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`)
        .flush(null, NO_CONTENT);
      await settle(harness.fixture);
      listRequest(harness.http).flush(managersPage([ANA]));
      await settle(harness.fixture);
    });
  });
});
