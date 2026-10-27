import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MANAGER, signInAs } from '../../testing/auth.fixtures';
import { SessionStore } from '../../core/auth/session.store';
import { provideUrbanIcons } from '../../core/theme/icons';
import { DashboardPage } from './dashboard.page';

function mockMatchMedia(): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: () => ({ matches: true }),
  });
}

describe('DashboardPage', () => {
  beforeEach(() => {
    mockMatchMedia();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideUrbanIcons()],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).match(() => true));

  it('mostra um cartao para cada modulo, com o titulo so para leitores de tela', async () => {
    const fixture = TestBed.createComponent(DashboardPage);
    await fixture.whenStable();
    const html: HTMLElement = fixture.nativeElement;

    const title = html.querySelector('h1');
    expect(title?.textContent?.trim()).toBe('Painel');
    expect(title?.classList).toContain('sr-only');
    expect(html.textContent).not.toContain('Gestão condominial');
    expect(html.querySelectorAll('mat-card.module')).toHaveLength(3);
    expect(html.querySelector('[data-testid="condominium-notice"]')).toBeNull();
  });

  it('avisa o sindico que ainda nao cadastrou o condominio', async () => {
    await signInAs(TestBed.inject(SessionStore), TestBed.inject(HttpTestingController), MANAGER);
    const fixture = TestBed.createComponent(DashboardPage);
    await fixture.whenStable();

    expect(
      fixture.nativeElement.querySelector('[data-testid="condominium-notice"]')?.textContent,
    ).toContain('O cadastro do seu condomínio ainda não foi feito');
  });

  it('nao mostra o aviso quando o condominio ja existe', async () => {
    await signInAs(TestBed.inject(SessionStore), TestBed.inject(HttpTestingController), {
      ...MANAGER,
      condominiumId: '0198-condominio',
    });
    const fixture = TestBed.createComponent(DashboardPage);
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('[data-testid="condominium-notice"]')).toBeNull();
  });
});
