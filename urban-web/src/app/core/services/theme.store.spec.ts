import { TestBed } from '@angular/core/testing';
import { ThemeStore } from './theme.store';

function systemPrefersDark(matches: boolean): void {
  window.matchMedia = ((query: string) => ({ matches, media: query })) as typeof window.matchMedia;
}

describe('ThemeStore', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('app-dark');
    TestBed.resetTestingModule();
  });

  afterEach(() => {
    Reflect.deleteProperty(window, 'matchMedia');
  });

  it('comeca no tema claro quando nao ha preferencia salva', () => {
    const store = TestBed.inject(ThemeStore);

    expect(store.mode()).toBe('light');
    expect(store.isDark()).toBe(false);
  });

  it('segue o tema escuro do sistema quando nao ha preferencia salva', () => {
    systemPrefersDark(true);

    const store = TestBed.inject(ThemeStore);

    expect(store.mode()).toBe('dark');
  });

  it('prefere a escolha salva ao tema do sistema', () => {
    systemPrefersDark(true);
    localStorage.setItem('urban.theme', 'light');

    const store = TestBed.inject(ThemeStore);

    expect(store.mode()).toBe('light');
  });

  it('restaura o tema salvo pelo usuario', () => {
    localStorage.setItem('urban.theme', 'dark');

    const store = TestBed.inject(ThemeStore);

    expect(store.mode()).toBe('dark');
  });

  it('alterna o tema, aplica a classe no html e persiste a escolha', () => {
    const store = TestBed.inject(ThemeStore);

    store.toggle();
    TestBed.tick();

    expect(store.isDark()).toBe(true);
    expect(document.documentElement.classList.contains('app-dark')).toBe(true);
    expect(localStorage.getItem('urban.theme')).toBe('dark');

    store.toggle();
    TestBed.tick();

    expect(store.isDark()).toBe(false);
    expect(document.documentElement.classList.contains('app-dark')).toBe(false);
    expect(localStorage.getItem('urban.theme')).toBe('light');
  });
});
