import { TestBed } from '@angular/core/testing';
import { DashboardPage } from './dashboard.page';

describe('DashboardPage', () => {
  it('mostra o titulo e um cartao para cada modulo', async () => {
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: () => ({ matches: true }),
    });

    const fixture = TestBed.createComponent(DashboardPage);
    await fixture.whenStable();
    const html: HTMLElement = fixture.nativeElement;

    expect(html.querySelector('h1')?.textContent).toContain('Urban');
    expect(html.querySelectorAll('mat-card')).toHaveLength(3);
  });
});
