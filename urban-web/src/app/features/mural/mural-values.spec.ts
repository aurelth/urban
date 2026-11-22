import { myAnnouncement } from '../../testing/announcements.fixtures';
import {
  accentClasses,
  categoryIcon,
  markAllLabel,
  markedNotice,
  muralScopeLabel,
  relativeTime,
  unreadSummary,
} from './mural-values';

const NOW = new Date('2026-11-22T15:00:00Z');

function ago(milliseconds: number): string {
  return new Date(NOW.getTime() - milliseconds).toISOString();
}

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;

describe('muralScopeLabel', () => {
  it('diz "Condomínio inteiro" para o aviso de todos', () => {
    expect(muralScopeLabel(myAnnouncement({ scope: 'condominium' }))).toBe('Condomínio inteiro');
  });

  it('diz o bloco no aviso de bloco', () => {
    expect(muralScopeLabel(myAnnouncement({ scope: 'block', block: 'A' }))).toBe('Bloco A');
  });

  it('fala da unidade do próprio morador, com o bloco quando há', () => {
    const withBlock = myAnnouncement({
      scope: 'unit',
      unit: { id: 'u-1', block: 'A', number: '101' },
    });
    const withoutBlock = myAnnouncement({
      scope: 'unit',
      unit: { id: 'u-2', block: null, number: '7' },
    });

    expect(muralScopeLabel(withBlock)).toBe('Sua unidade 101 do bloco A');
    expect(muralScopeLabel(withoutBlock)).toBe('Sua unidade 7');
  });

  it('não quebra se a API não trouxer a unidade do aviso de unidade', () => {
    expect(muralScopeLabel(myAnnouncement({ scope: 'unit', unit: null }))).toBe('Sua unidade');
  });
});

describe('relativeTime', () => {
  it.each([
    ['agora mesmo', 0, 'agora há pouco'],
    ['30 segundos', 30_000, 'agora há pouco'],
    ['1 minuto', MINUTE, 'há 1 min'],
    ['5 minutos', 5 * MINUTE, 'há 5 min'],
    ['59 minutos', 59 * MINUTE, 'há 59 min'],
    ['1 hora', HOUR, 'há 1 h'],
    ['3 horas e meia', 3.5 * HOUR, 'há 3 h'],
    ['23 horas', 23 * HOUR, 'há 23 h'],
  ])('%s atrás: "%s"', (_name, elapsed, expected) => {
    expect(relativeTime(ago(elapsed), NOW)).toBe(expected);
  });

  it('diz "ontem" para o dia anterior', () => {
    expect(relativeTime('2026-11-21T12:00:00Z', NOW)).toBe('ontem');
  });

  it.each([
    ['2026-11-20T12:00:00Z', 'há 2 dias'],
    ['2026-11-16T12:00:00Z', 'há 6 dias'],
  ])('%s: "%s"', (iso, expected) => {
    expect(relativeTime(iso, NOW)).toBe(expected);
  });

  it('passada uma semana, mostra a data de Brasília', () => {
    expect(relativeTime('2026-11-15T12:00:00Z', NOW)).toBe('15/11/2026');
    expect(relativeTime('2026-01-02T01:30:00Z', NOW)).toBe('01/01/2026');
  });

  it('conta os dias pelo calendário de Brasília, não pelo de Greenwich', () => {
    // 22/11 às 23:00 em Brasília já é 23/11 em Greenwich; o aviso de 21/11 às 17:00 continua sendo de "ontem".
    const lateEvening = new Date('2026-11-23T02:00:00Z');

    expect(relativeTime('2026-11-21T20:00:00Z', lateEvening)).toBe('ontem');
  });

  it('trata um aviso do "futuro" (relógio do aparelho atrasado) como agora há pouco', () => {
    expect(relativeTime(ago(-5 * MINUTE), NOW)).toBe('agora há pouco');
  });

  it('devolve o texto como veio quando a data não é válida', () => {
    expect(relativeTime('ontem à noite', NOW)).toBe('ontem à noite');
  });
});

describe('unreadSummary', () => {
  it.each([
    [0, 'Você está em dia'],
    [1, 'Você tem 1 aviso novo'],
    [2, 'Você tem 2 avisos novos'],
    [25, 'Você tem 25 avisos novos'],
  ])('%i não lidos: "%s"', (unread, expected) => {
    expect(unreadSummary(unread)).toBe(expected);
  });
});

describe('markAllLabel', () => {
  it.each([
    [null, 'Marcar todos como lidos'],
    ['general', 'Marcar avisos gerais como lidos'],
    ['maintenance', 'Marcar avisos de manutenção como lidos'],
    ['important', 'Marcar avisos importantes como lidos'],
  ] as const)('categoria %s: "%s"', (category, expected) => {
    expect(markAllLabel(category)).toBe(expected);
  });
});

describe('markedNotice', () => {
  it.each([
    [0, 'Nenhum aviso novo para marcar'],
    [1, '1 aviso marcado como lido'],
    [4, '4 avisos marcados como lidos'],
  ])('%i marcados: "%s"', (marked, expected) => {
    expect(markedNotice(marked)).toBe(expected);
  });
});

describe('categoryIcon e accentClasses', () => {
  it('dá a cada categoria um ícone próprio', () => {
    expect(categoryIcon('general')).toBe('info');
    expect(categoryIcon('maintenance')).toBe('build');
    expect(categoryIcon('important')).toBe('warning');
  });

  it('pinta a barra do cartão com a cor da categoria, a mesma da etiqueta', () => {
    expect(accentClasses('general')).toBe('bg-tag-blue');
    expect(accentClasses('maintenance')).toBe('bg-tag-amber');
    expect(accentClasses('important')).toBe('bg-tag-red');
  });
});
