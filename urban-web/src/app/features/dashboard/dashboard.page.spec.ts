import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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
  });
});
