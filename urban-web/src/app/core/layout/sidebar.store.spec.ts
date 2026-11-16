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

  describe('seções recolhidas do menu', () => {
    it('começa com todas as seções abertas', () => {
      const store = TestBed.inject(SidebarStore);

      expect(store.collapsedGroups()).toEqual([]);
      expect(store.isGroupCollapsed('finance')).toBe(false);
    });

    it('recolhe uma seção, abre de novo ao repetir e guarda a escolha', () => {
      const store = TestBed.inject(SidebarStore);

      store.toggleGroup('finance');

      expect(store.isGroupCollapsed('finance')).toBe(true);
      expect(store.isGroupCollapsed('condominium')).toBe(false);
      expect(localStorage.getItem('urban.sidebar-groups')).toBe('["finance"]');

      store.toggleGroup('finance');

      expect(store.isGroupCollapsed('finance')).toBe(false);
      expect(localStorage.getItem('urban.sidebar-groups')).toBe('[]');
    });

    it('recolhe várias seções de forma independente', () => {
      const store = TestBed.inject(SidebarStore);

      store.toggleGroup('condominium');
      store.toggleGroup('finance');
      store.toggleGroup('condominium');

      expect(store.collapsedGroups()).toEqual(['finance']);
    });

    it('restaura as seções recolhidas na próxima visita', () => {
      localStorage.setItem('urban.sidebar-groups', '["condominium","finance"]');

      const store = TestBed.inject(SidebarStore);

      expect(store.isGroupCollapsed('condominium')).toBe(true);
      expect(store.isGroupCollapsed('finance')).toBe(true);
    });

    it.each([
      ['texto que não é JSON', 'qualquer-coisa'],
      ['JSON que não é uma lista', '{"finance":true}'],
      ['lista com valores que não são texto', '[1,null,{"a":1}]'],
    ])('ignora o valor salvo quando é %s', (_name, saved) => {
      localStorage.setItem('urban.sidebar-groups', saved);

      const store = TestBed.inject(SidebarStore);

      expect(store.collapsedGroups()).toEqual([]);
    });

    it('guarda só os textos de uma lista mista, sem repetir', () => {
      localStorage.setItem('urban.sidebar-groups', '["finance",3,"finance"]');

      const store = TestBed.inject(SidebarStore);

      expect(store.collapsedGroups()).toEqual(['finance']);
    });

    it('não mistura com a escolha de recolher o menu inteiro', () => {
      const store = TestBed.inject(SidebarStore);

      store.toggleGroup('finance');
      store.toggle();

      expect(localStorage.getItem('urban.sidebar')).toBe('collapsed');
      expect(localStorage.getItem('urban.sidebar-groups')).toBe('["finance"]');
    });

    it('segue funcionando, só sem lembrar, quando o navegador não deixa guardar', () => {
      jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('bloqueado');
      });
      jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('bloqueado');
      });

      const store = TestBed.inject(SidebarStore);
      store.toggleGroup('finance');

      expect(store.isGroupCollapsed('finance')).toBe(true);
    });
  });
});
