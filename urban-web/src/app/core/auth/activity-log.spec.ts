import { jest } from '@jest/globals';
import { TestBed } from '@angular/core/testing';
import { ActivityLog } from './activity-log';

const KEY = 'urban.last-activity';

describe('ActivityLog', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('comeca sem atividade registrada', () => {
    expect(TestBed.inject(ActivityLog).lastActivity()).toBe(0);
  });

  it('guarda o instante da ultima atividade', () => {
    const log = TestBed.inject(ActivityLog);

    log.touch(10_000);

    expect(log.lastActivity()).toBe(10_000);
    expect(localStorage.getItem(KEY)).toBe('10000');
  });

  it('grava no armazenamento no maximo a cada 5 segundos, mas nao perde o instante', () => {
    const log = TestBed.inject(ActivityLog);

    log.touch(10_000);
    log.touch(12_000);

    expect(localStorage.getItem(KEY)).toBe('10000');
    expect(log.lastActivity()).toBe(12_000);

    log.touch(15_000);

    expect(localStorage.getItem(KEY)).toBe('15000');
  });

  it('grava na hora quando for forcado, como ao entrar na conta', () => {
    const log = TestBed.inject(ActivityLog);
    log.touch(10_000);

    log.touch(11_000, true);

    expect(localStorage.getItem(KEY)).toBe('11000');
  });

  it('enxerga a atividade de outra aba do mesmo navegador', () => {
    const log = TestBed.inject(ActivityLog);
    log.touch(10_000);

    localStorage.setItem(KEY, '50000');

    expect(log.lastActivity()).toBe(50_000);
  });

  it('ignora um valor guardado que nao e um numero', () => {
    const log = TestBed.inject(ActivityLog);
    log.touch(10_000);

    localStorage.setItem(KEY, 'lixo');

    expect(log.lastActivity()).toBe(10_000);
  });

  it('segue so com a memoria desta aba quando o armazenamento falha', () => {
    jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    const log = TestBed.inject(ActivityLog);

    log.touch(10_000);

    expect(log.lastActivity()).toBe(10_000);
  });
});
