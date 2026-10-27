import { TestBed } from '@angular/core/testing';
import { provideTheme } from './theme.providers';

describe('provideTheme', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('app-dark');
    TestBed.resetTestingModule();
  });

  it('aplica o tema salvo ao abrir o app, sem que nenhuma tela peça o ThemeStore', () => {
    localStorage.setItem('urban.theme', 'dark');
    TestBed.configureTestingModule({ providers: [provideTheme()] });

    TestBed.tick();

    expect(document.documentElement.classList.contains('app-dark')).toBe(true);
  });

  it('mantém o tema claro quando a pessoa escolheu claro', () => {
    localStorage.setItem('urban.theme', 'light');
    TestBed.configureTestingModule({ providers: [provideTheme()] });

    TestBed.tick();

    expect(document.documentElement.classList.contains('app-dark')).toBe(false);
  });
});
