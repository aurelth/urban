import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { RegistrationDraft, RegistrationDraftData } from './registration-draft';

const USER = '01a176be-52fe-7784-a4d6-ed99baca79c9';
const OTHER_USER = '01a176be-52fe-7784-a4d6-ed99baca7aaa';
const KEY = `urban.registration-draft.${USER}`;
const NOW = Date.UTC(2026, 9, 29, 12, 0, 0);
const DAY_MS = 24 * 60 * 60 * 1000;

const ADDRESS = {
  postalCode: '89010-000',
  street: 'Rua das Flores',
  number: '120',
  complement: 'Apto 42',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const DRAFT: RegistrationDraftData = {
  manager: {
    fullName: 'Marina Souza',
    displayName: 'Marina',
    phone: '(47) 99123-4567',
    address: ADDRESS,
  },
  condominium: {
    name: 'Residencial das Flores',
    cnpj: '11.222.333/0001-81',
    address: { ...ADDRESS, postalCode: '89012-100', complement: '' },
  },
  sameAddress: false,
};

describe('RegistrationDraft', () => {
  let draft: RegistrationDraft;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    draft = TestBed.inject(RegistrationDraft);
  });

  afterEach(() => {
    jest.restoreAllMocks();
    localStorage.clear();
  });

  it('nao tem rascunho enquanto nada foi guardado', () => {
    expect(draft.load(USER, NOW)).toBeNull();
  });

  it('guarda o rascunho e devolve o mesmo conteudo', () => {
    draft.save(USER, DRAFT, NOW);

    expect(draft.load(USER, NOW + 1_000)).toEqual(DRAFT);
  });

  it('usa uma chave por usuario, para duas pessoas no mesmo navegador nao verem o rascunho uma da outra', () => {
    draft.save(USER, DRAFT, NOW);

    expect(localStorage.getItem(KEY)).not.toBeNull();
    expect(draft.load(OTHER_USER, NOW)).toBeNull();
  });

  it('guarda o instante em que foi salvo, para poder vencer', () => {
    draft.save(USER, DRAFT, NOW);

    expect(JSON.parse(localStorage.getItem(KEY)!)).toMatchObject({ savedAt: NOW });
  });

  it('vale por 7 dias e depois some, apagando o que estava guardado', () => {
    draft.save(USER, DRAFT, NOW);

    expect(draft.load(USER, NOW + 7 * DAY_MS - 1)).toEqual(DRAFT);
    expect(draft.load(USER, NOW + 7 * DAY_MS)).toBeNull();
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('um novo salvamento renova a validade', () => {
    draft.save(USER, DRAFT, NOW);
    draft.save(USER, DRAFT, NOW + 6 * DAY_MS);

    expect(draft.load(USER, NOW + 10 * DAY_MS)).toEqual(DRAFT);
  });

  it.each([
    ['texto que nao e JSON', 'isto nao e json'],
    ['JSON que nao e um objeto', '[1,2,3]'],
    ['sem o instante do salvamento', JSON.stringify({ data: DRAFT })],
    ['com o instante no futuro', JSON.stringify({ savedAt: NOW + DAY_MS, data: DRAFT })],
    ['com o instante em texto', JSON.stringify({ savedAt: 'ontem', data: DRAFT })],
    ['sem os dados', JSON.stringify({ savedAt: NOW })],
    [
      'com o bloco do sindico faltando',
      JSON.stringify({ savedAt: NOW, data: { ...DRAFT, manager: null } }),
    ],
    [
      'com campo de texto que nao e texto',
      JSON.stringify({
        savedAt: NOW,
        data: { ...DRAFT, manager: { ...DRAFT.manager, phone: 4799123 } },
      }),
    ],
    [
      'com sameAddress que nao e verdadeiro ou falso',
      JSON.stringify({ savedAt: NOW, data: { ...DRAFT, sameAddress: 'sim' } }),
    ],
  ])('ignora e apaga o que esta guardado %s', (_name, stored) => {
    localStorage.setItem(KEY, stored);

    expect(draft.load(USER, NOW)).toBeNull();
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('ignora os campos que nao fazem parte do rascunho, mesmo que alguem os grave a mao', () => {
    const tampered = {
      ...DRAFT,
      manager: { ...DRAFT.manager, cpf: '52998224725', birthDate: '15061980', password: 'x' },
    };
    localStorage.setItem(KEY, JSON.stringify({ savedAt: NOW, data: tampered }));

    const loaded = draft.load(USER, NOW);

    expect(loaded).toEqual(DRAFT);
    expect(JSON.stringify(loaded)).not.toContain('52998224725');
  });

  it('clear apaga so o rascunho do usuario pedido', () => {
    draft.save(USER, DRAFT, NOW);
    draft.save(OTHER_USER, DRAFT, NOW);

    draft.clear(USER);

    expect(draft.load(USER, NOW)).toBeNull();
    expect(draft.load(OTHER_USER, NOW)).toEqual(DRAFT);
  });

  it('clearAll apaga o rascunho de todos e deixa o resto do armazenamento em paz', () => {
    draft.save(USER, DRAFT, NOW);
    draft.save(OTHER_USER, DRAFT, NOW);
    localStorage.setItem('urban.last-activity', '10000');

    draft.clearAll();

    expect(draft.load(USER, NOW)).toBeNull();
    expect(draft.load(OTHER_USER, NOW)).toBeNull();
    expect(localStorage.getItem('urban.last-activity')).toBe('10000');
  });

  describe('armazenamento indisponivel', () => {
    it('cheio: salvar nao quebra a tela', () => {
      jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new DOMException('cheio', 'QuotaExceededError');
      });

      expect(() => draft.save(USER, DRAFT, NOW)).not.toThrow();
    });

    it('bloqueado: ler devolve nada, sem quebrar', () => {
      jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new DOMException('bloqueado', 'SecurityError');
      });

      expect(draft.load(USER, NOW)).toBeNull();
    });

    it('bloqueado: apagar nao quebra a tela', () => {
      jest.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
        throw new DOMException('bloqueado', 'SecurityError');
      });

      expect(() => draft.clear(USER)).not.toThrow();
      expect(() => draft.clearAll()).not.toThrow();
    });

    it('bloqueado: nem o acesso ao localStorage quebra', () => {
      jest.spyOn(window, 'localStorage', 'get').mockImplementation(() => {
        throw new DOMException('bloqueado', 'SecurityError');
      });

      expect(() => draft.save(USER, DRAFT, NOW)).not.toThrow();
      expect(draft.load(USER, NOW)).toBeNull();
      expect(() => draft.clear(USER)).not.toThrow();
      expect(() => draft.clearAll()).not.toThrow();
    });
  });
});
