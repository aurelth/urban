import { MessagesByCode } from '../../core/http/problem-message';
import { PASSWORD_POLICY_MESSAGES } from '../auth/password-messages';

const LOCKED_OUT = 'Conta bloqueada por muitas tentativas. Tente novamente em 15 minutos';

/** Texto da troca de senha para cada código que a API pode devolver. */
export const CHANGE_PASSWORD_MESSAGES: MessagesByCode = {
  'identity.password.incorrect': 'Senha atual incorreta',
  'identity.password.unchanged': 'A nova senha precisa ser diferente da atual',
  ...PASSWORD_POLICY_MESSAGES,
  'identity.login.locked_out': LOCKED_OUT,
  'identity.user.account_suspended': 'Acesso suspenso. Fale com o suporte',
  'identity.user.account_deactivated': 'Conta desativada. Fale com o síndico',
};

/** Texto do pedido de troca de e-mail para cada código que a API pode devolver. */
export const CHANGE_EMAIL_MESSAGES: MessagesByCode = {
  'identity.password.incorrect': 'Senha incorreta',
  'identity.email.invalid': 'Informe um e-mail válido',
  'identity.email.unchanged': 'Este já é o e-mail da sua conta',
  'identity.login.locked_out': LOCKED_OUT,
  'identity.user.account_suspended': 'Acesso suspenso. Fale com o suporte',
  'identity.user.account_deactivated': 'Conta desativada. Fale com o síndico',
};
