import { initialsOf } from './initials';

describe('initialsOf', () => {
  it.each([
    ['Marina', 'M'],
    ['Marina Souza', 'MS'],
    ['Ana Maria Souza', 'AS'],
    ['  ana  ', 'A'],
    ['álvaro costa', 'ÁC'],
  ])('usa as iniciais do nome escolhido (%p)', (displayName, expected) => {
    expect(initialsOf({ displayName, email: 'x@example.com' })).toBe(expected);
  });

  it.each([
    ['sindica@example.com', 'S'],
    ['maria.silva@example.com', 'MS'],
    ['joao_pedro-lima@example.com', 'JL'],
  ])('usa o começo do e-mail enquanto a pessoa não escolheu um nome (%p)', (email, expected) => {
    expect(initialsOf({ displayName: null, email })).toBe(expected);
  });

  it.each([[''], ['   ']])('trata nome em branco (%p) como sem nome', (displayName) => {
    expect(initialsOf({ displayName, email: 'sindica@example.com' })).toBe('S');
  });

  it('mostra um ponto de interrogação quando não há de onde tirar uma letra', () => {
    expect(initialsOf({ displayName: null, email: '@example.com' })).toBe('?');
  });
});
