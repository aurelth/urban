import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { SidebarStore } from './sidebar.store';

describe('SidebarStore', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  afterEach(() => jest.restoreAllMocks());

  it('começa fixado (aberto, com os nomes) quando não há escolha salva', () => {
    const store = TestBed.inject(SidebarStore);

    expect(store.collapsed()).toBe(false);
  });

  it('restaura a escolha salva de recolher', () => {
    localStorage.setItem('urban.sidebar', 'collapsed');

    const store = TestBed.inject(SidebarStore);

    expect(store.collapsed()).toBe(true);
  });

  it('restaura a escolha salva de fixar', () => {
    localStorage.setItem('urban.sidebar', 'pinned');

    const store = TestBed.inject(SidebarStore);

    expect(store.collapsed()).toBe(false);
  });

  it('ignora um valor salvo que não conhece', () => {
    localStorage.setItem('urban.sidebar', 'qualquer-coisa');

    const store = TestBed.inject(SidebarStore);

    expect(store.collapsed()).toBe(false);
  });

  it('alterna entre recolhido e fixado e guarda a escolha', () => {
    const store = TestBed.inject(SidebarStore);

    store.toggle();

    expect(store.collapsed()).toBe(true);
    expect(localStorage.getItem('urban.sidebar')).toBe('collapsed');

    store.toggle();

    expect(store.collapsed()).toBe(false);
    expect(localStorage.getItem('urban.sidebar')).toBe('pinned');
  });

  it('aceita a escolha direta', () => {
    const store = TestBed.inject(SidebarStore);

    store.set(true);
    expect(store.collapsed()).toBe(true);

    store.set(false);
    expect(store.collapsed()).toBe(false);
  });

  it('segue funcionando, só sem lembrar, quando o navegador não deixa guardar', () => {
    jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });

    const store = TestBed.inject(SidebarStore);
    store.toggle();

    expect(store.collapsed()).toBe(true);
  });
});
