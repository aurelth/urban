import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, Routes, provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { MANAGER, REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import { CurrentUser } from '../auth/auth.models';
import { SessionStore } from '../auth/session.store';
import { provideUrbanIcons } from '../theme/icons';
import { ShellLayout } from './shell.layout';

@Component({ template: '' })
class Page {}

const ROUTES: Routes = [
  'dashboard',
  'unidades',
  'financeiro/despesas',
  'financeiro/tarifas',
  'financeiro/leituras',
  'financeiro/lancamentos',
  'condominio/meus-dados',
  'conta',
].map((path) => ({ path, component: Page }));

async function setup(user: CurrentUser = MANAGER): Promise<{
  fixture: ComponentFixture<ShellLayout>;
  store: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  router: Router;
  html: HTMLElement;
}> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ROUTES),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
    ],
  });
  const store = TestBed.inject(SessionStore);
  const http = TestBed.inject(HttpTestingController);
  await signInAs(store, http, user);
  const fixture = TestBed.createComponent(ShellLayout);
  await fixture.whenStable();
  return { fixture, store, http, router: TestBed.inject(Router), html: fixture.nativeElement };
}

function text(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function byTestId(html: ParentNode, name: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${name}"]`);
}

/** Faz o jsdom (que não tem `matchMedia`) responder que a tela tem 1024 px ou mais. */
function useDesktopScreen(): void {
  window.matchMedia = ((query: string) => ({
    matches: true,
    media: query,
    addListener: () => undefined,
    removeListener: () => undefined,
  })) as unknown as typeof window.matchMedia;
}

async function openUserMenu(
  fixture: ComponentFixture<ShellLayout>,
  html: HTMLElement,
): Promise<HTMLElement> {
  byTestId(html, 'user-menu')!.click();
  await fixture.whenStable();
  TestBed.tick();
  return document.querySelector<HTMLElement>('.mat-mdc-menu-panel')!;
}

describe('ShellLayout', () => {
  afterEach(() => {
    Reflect.deleteProperty(window, 'matchMedia');
    localStorage.clear();
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('cabeçalho', () => {
    it('mostra o e-mail e o papel, separados por hífen, enquanto a pessoa não escolheu um nome', async () => {
      const { html } = await setup();

      expect(text(byTestId(html, 'current-user'))).toBe('sindica@example.com - Síndico');
    });

    it('cumprimenta pelo nome escolhido e mostra o papel, sem o e-mail', async () => {
      const { html } = await setup(REGISTERED_MANAGER);

      expect(text(byTestId(html, 'current-user'))).toBe('Olá Marina - Síndico');
    });

    it('mostra as iniciais no avatar', async () => {
      const marina = await setup(REGISTERED_MANAGER);
      expect(text(byTestId(marina.html, 'user-menu'))).toBe('M');
      TestBed.resetTestingModule();

      const semNome = await setup();
      expect(text(byTestId(semNome.html, 'user-menu'))).toBe('S');
    });

    it('deixa o nome do avatar claro para leitores de tela', async () => {
      const { html } = await setup(REGISTERED_MANAGER);

      expect(byTestId(html, 'user-menu')?.getAttribute('aria-label')).toBe('Menu do usuário');
    });

    it('mantém o botão de tema', async () => {
      const { html } = await setup();

      expect(html.querySelector('button[aria-label="Usar tema escuro"]')).not.toBeNull();
    });

    it('leva a logo para a página inicial', async () => {
      const { html } = await setup();

      const logos = [...html.querySelectorAll('urban-brand-link a')];

      expect(logos.length).toBeGreaterThan(0);
      for (const logo of logos) {
        expect(logo.getAttribute('href')).toBe('/');
        expect(text(logo)).toBe('Urban');
      }
    });
  });

  describe('menu do avatar', () => {
    it('fica fechado até a pessoa abrir', async () => {
      const { html } = await setup(REGISTERED_MANAGER);

      expect(document.querySelector('.mat-mdc-menu-panel')).toBeNull();
      expect(byTestId(html, 'user-menu')?.getAttribute('aria-expanded')).toBe('false');
    });

    it('repete a saudação completa, que some do cabeçalho no celular', async () => {
      const { fixture, html } = await setup(REGISTERED_MANAGER);

      const panel = await openUserMenu(fixture, html);

      expect(text(byTestId(panel, 'menu-user'))).toBe('Olá Marina - Síndico');
    });

    it('leva o síndico com condomínio aos dados, à conta e à saída, nessa ordem', async () => {
      const { fixture, html } = await setup(REGISTERED_MANAGER);

      const panel = await openUserMenu(fixture, html);
      const items = [...panel.querySelectorAll<HTMLElement>('[role="menuitem"]')];

      expect(items.map((item) => item.dataset['testid'])).toEqual([
        'menu-profile',
        'menu-account',
        'sign-out',
      ]);
      expect(items.map(text)).toEqual(['Meus dados', 'Minha conta', 'Sair']);
      expect(byTestId(panel, 'menu-profile')?.getAttribute('href')).toBe('/condominio/meus-dados');
      expect(byTestId(panel, 'menu-account')?.getAttribute('href')).toBe('/conta');
    });

    it('não mostra os dados do condomínio ao síndico que ainda não o cadastrou', async () => {
      const { fixture, html } = await setup(MANAGER);

      const panel = await openUserMenu(fixture, html);

      expect(byTestId(panel, 'menu-profile')).toBeNull();
      expect(byTestId(panel, 'menu-account')).not.toBeNull();
      expect(byTestId(panel, 'sign-out')).not.toBeNull();
    });

    it.each([
      ['morador', 'Resident' as const],
      ['administrador', 'SuperAdmin' as const],
    ])('mostra só a conta e a saída para %s', async (_name, role) => {
      const { fixture, html } = await setup({ ...REGISTERED_MANAGER, role });

      const panel = await openUserMenu(fixture, html);

      expect(byTestId(panel, 'menu-profile')).toBeNull();
      expect(byTestId(panel, 'menu-account')).not.toBeNull();
      expect(byTestId(panel, 'sign-out')).not.toBeNull();
    });

    it('sai pela API e volta para a entrada', async () => {
      const { fixture, http, router, html } = await setup();
      const navigate = jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
      const panel = await openUserMenu(fixture, html);

      byTestId(panel, 'sign-out')!.click();
      http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith('/entrar');
    });
  });

  describe('sessão', () => {
    it('leva para a entrada, guardando o destino, quando a sessão expira', async () => {
      const { fixture, store, router } = await setup();
      const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);

      store.expire();
      TestBed.tick();
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(['/entrar'], {
        queryParams: { returnUrl: router.url },
      });
    });

    it('não guarda destino quando a pessoa saiu de propósito', async () => {
      const { fixture, http, router, html } = await setup();
      const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);
      jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
      const panel = await openUserMenu(fixture, html);

      byTestId(panel, 'sign-out')!.click();
      http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
      await fixture.whenStable();
      TestBed.tick();

      expect(navigate).not.toHaveBeenCalled();
    });
  });

  describe('menu lateral', () => {
    function sideMenu(html: HTMLElement): HTMLElement | null {
      return html.querySelector<HTMLElement>('nav[aria-label="Menu principal"]');
    }

    function links(html: HTMLElement): HTMLAnchorElement[] {
      return [...html.querySelectorAll<HTMLAnchorElement>('nav a[data-testid^="nav-"]')];
    }

    it('leva o síndico com condomínio às telas, agrupadas e na ordem do trabalho do mês', async () => {
      const { html } = await setup(REGISTERED_MANAGER);

      expect(links(html).map((link) => link.dataset['testid'])).toEqual([
        'nav-dashboard',
        'nav-units',
        'nav-expenses',
        'nav-tariffs',
        'nav-readings',
        'nav-entries',
      ]);
      expect(links(html).map(text)).toEqual([
        'Painel',
        'Unidades',
        'Despesas',
        'Tarifas',
        'Leituras',
        'Lançamentos',
      ]);
      expect(links(html).map((link) => link.getAttribute('href'))).toEqual([
        '/dashboard',
        '/unidades',
        '/financeiro/despesas',
        '/financeiro/tarifas',
        '/financeiro/leituras',
        '/financeiro/lancamentos',
      ]);
      expect([...sideMenu(html)!.querySelectorAll('[data-testid="nav-group"]')].map(text)).toEqual([
        'Condomínio',
        'Financeiro',
      ]);
    });

    it.each([
      ['síndico sem condomínio', MANAGER],
      ['morador', { ...REGISTERED_MANAGER, role: 'Resident' as const }],
      ['administrador', { ...REGISTERED_MANAGER, role: 'SuperAdmin' as const }],
    ])('não tem menu lateral nem botão para abri-lo para %s', async (_name, user) => {
      const { html } = await setup(user);

      expect(sideMenu(html)).toBeNull();
      expect(byTestId(html, 'menu-toggle')).toBeNull();
      expect(byTestId(html, 'user-menu')).not.toBeNull();
    });

    it('marca a tela aberta como a página atual', async () => {
      const { fixture, router, html } = await setup(REGISTERED_MANAGER);

      await router.navigateByUrl('/unidades');
      await fixture.whenStable();
      TestBed.tick();

      expect(byTestId(html, 'nav-units')?.getAttribute('aria-current')).toBe('page');
      expect(byTestId(html, 'nav-expenses')?.getAttribute('aria-current')).toBeNull();
    });

    describe('no computador, onde ele é fixo e pode ser recolhido', () => {
      beforeEach(useDesktopScreen);

      function sidenav(html: HTMLElement): HTMLElement {
        return html.querySelector<HTMLElement>('mat-sidenav')!;
      }

      function width(html: HTMLElement): string {
        return sidenav(html).style.getPropertyValue('--mat-sidenav-container-width');
      }

      function labels(html: HTMLElement): HTMLElement[] {
        return [...html.querySelectorAll<HTMLElement>('[data-testid="nav-label"]')];
      }

      async function toggle(
        fixture: ComponentFixture<ShellLayout>,
        html: HTMLElement,
      ): Promise<void> {
        byTestId(html, 'menu-toggle')!.click();
        await fixture.whenStable();
        TestBed.tick();
      }

      it('nasce aberto, com os nomes, e o botão do cabeçalho oferece recolher', async () => {
        const { html } = await setup(REGISTERED_MANAGER);
        const button = byTestId(html, 'menu-toggle')!;

        expect(sidenav(html).classList.contains('mat-drawer-opened')).toBe(true);
        expect(sidenav(html).classList.contains('mat-drawer-side')).toBe(true);
        expect(width(html)).toBe('256px');
        expect(button.getAttribute('aria-label')).toBe('Recolher menu');
        expect(button.getAttribute('aria-expanded')).toBe('true');
        expect(labels(html).every((label) => !label.classList.contains('sr-only'))).toBe(true);
      });

      it('deixa a logo só no topo do menu, e não repete no cabeçalho', async () => {
        const { html } = await setup(REGISTERED_MANAGER);

        expect(html.querySelector('mat-toolbar urban-brand-link')).toBeNull();
        expect(text(html.querySelector('mat-sidenav urban-brand-link a'))).toBe('Urban');
      });

      it('recolhe numa coluna só de ícones, sem tirar os nomes dos leitores de tela', async () => {
        const { fixture, html } = await setup(REGISTERED_MANAGER);

        await toggle(fixture, html);

        const button = byTestId(html, 'menu-toggle')!;
        expect(sidenav(html).classList.contains('mat-drawer-opened')).toBe(true);
        expect(width(html)).toBe('72px');
        expect(button.getAttribute('aria-label')).toBe('Fixar menu');
        expect(button.getAttribute('aria-expanded')).toBe('false');
        expect(labels(html).map(text)).toEqual([
          'Painel',
          'Unidades',
          'Despesas',
          'Tarifas',
          'Leituras',
          'Lançamentos',
        ]);
        expect(labels(html).every((label) => label.classList.contains('sr-only'))).toBe(true);
        expect(html.querySelectorAll('[data-testid="nav-group"]')).toHaveLength(0);
        expect(text(html.querySelector('mat-sidenav urban-brand-link a'))).toBe('');
        expect(
          html.querySelector('mat-sidenav urban-brand-link a')?.getAttribute('aria-label'),
        ).toBe('Urban, ir para a página inicial');
      });

      it('fixa de novo, voltando os nomes e os títulos dos grupos', async () => {
        const { fixture, html } = await setup(REGISTERED_MANAGER);
        await toggle(fixture, html);

        await toggle(fixture, html);

        expect(width(html)).toBe('256px');
        expect(byTestId(html, 'menu-toggle')!.getAttribute('aria-label')).toBe('Recolher menu');
        expect(labels(html).every((label) => !label.classList.contains('sr-only'))).toBe(true);
        expect([...html.querySelectorAll('[data-testid="nav-group"]')].map(text)).toEqual([
          'Condomínio',
          'Financeiro',
        ]);
      });

      it('mostra o nome de cada item numa dica quando está recolhido', async () => {
        const { fixture, html } = await setup(REGISTERED_MANAGER);
        await toggle(fixture, html);

        expect(byTestId(html, 'nav-units')!.getAttribute('aria-describedby')).not.toBeNull();
      });

      it('lembra a escolha na próxima visita', async () => {
        const first = await setup(REGISTERED_MANAGER);
        await toggle(first.fixture, first.html);
        expect(localStorage.getItem('urban.sidebar')).toBe('collapsed');
        TestBed.resetTestingModule();

        const { html } = await setup(REGISTERED_MANAGER);

        expect(width(html)).toBe('72px');
        expect(byTestId(html, 'menu-toggle')!.getAttribute('aria-label')).toBe('Fixar menu');
      });

      it('não fecha ao escolher uma tela', async () => {
        const { fixture, html } = await setup(REGISTERED_MANAGER);

        byTestId(html, 'nav-units')!.click();
        await fixture.whenStable();
        TestBed.tick();

        expect(sidenav(html).classList.contains('mat-drawer-opened')).toBe(true);
      });
    });

    it('no celular ignora a escolha de recolher: a gaveta sempre mostra os nomes', async () => {
      localStorage.setItem('urban.sidebar', 'collapsed');
      const { fixture, html } = await setup(REGISTERED_MANAGER);
      const toggle = byTestId(html, 'menu-toggle')!;

      toggle.click();
      await fixture.whenStable();
      TestBed.tick();

      expect(toggle.getAttribute('aria-label')).toBe('Fechar menu');
      expect(
        html
          .querySelector<HTMLElement>('mat-sidenav')!
          .style.getPropertyValue('--mat-sidenav-container-width'),
      ).toBe('256px');
      expect(
        [...html.querySelectorAll('[data-testid="nav-label"]')].some((label) =>
          label.classList.contains('sr-only'),
        ),
      ).toBe(false);
      expect(html.querySelectorAll('[data-testid="nav-group"]')).toHaveLength(2);
    });

    describe('no celular, onde ele é uma gaveta', () => {
      function drawer(html: HTMLElement): HTMLElement {
        return html.querySelector<HTMLElement>('mat-sidenav')!;
      }

      it('começa fechada e o botão do cabeçalho a abre e a fecha', async () => {
        const { fixture, html } = await setup(REGISTERED_MANAGER);
        const toggle = byTestId(html, 'menu-toggle')!;

        expect(drawer(html).classList.contains('mat-drawer-opened')).toBe(false);
        expect(toggle.getAttribute('aria-label')).toBe('Abrir menu');
        expect(toggle.getAttribute('aria-expanded')).toBe('false');

        toggle.click();
        await fixture.whenStable();
        TestBed.tick();

        expect(drawer(html).classList.contains('mat-drawer-opened')).toBe(true);
        expect(toggle.getAttribute('aria-label')).toBe('Fechar menu');
        expect(toggle.getAttribute('aria-expanded')).toBe('true');

        toggle.click();
        await fixture.whenStable();
        TestBed.tick();

        expect(drawer(html).classList.contains('mat-drawer-opened')).toBe(false);
      });

      it('fecha ao escolher uma tela', async () => {
        const { fixture, html } = await setup(REGISTERED_MANAGER);
        byTestId(html, 'menu-toggle')!.click();
        await fixture.whenStable();
        TestBed.tick();

        byTestId(html, 'nav-units')!.click();
        await fixture.whenStable();
        TestBed.tick();

        expect(drawer(html).classList.contains('mat-drawer-opened')).toBe(false);
      });
    });
  });
});
