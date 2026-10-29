import { CurrentUser } from './auth.models';

/** O que o cabeçalho mostra da conta: "Olá Ana!" ou, enquanto a pessoa não escolheu um nome, o e-mail. */
export function accountLabel(user: Pick<CurrentUser, 'displayName' | 'email'>): string {
  return user.displayName ? `Olá ${user.displayName}!` : user.email;
}
