import { accountLabel } from './account-label';

describe('accountLabel', () => {
  it('cumprimenta pelo nome que a pessoa prefere', () => {
    expect(accountLabel({ displayName: 'Marina', email: 'marina@example.com' })).toBe(
      'Olá Marina!',
    );
  });

  it('mostra o e-mail enquanto a pessoa ainda não escolheu um nome', () => {
    expect(accountLabel({ displayName: null, email: 'marina@example.com' })).toBe(
      'marina@example.com',
    );
  });

  it('trata nome em branco como ausente', () => {
    expect(accountLabel({ displayName: '', email: 'marina@example.com' })).toBe(
      'marina@example.com',
    );
  });
});
