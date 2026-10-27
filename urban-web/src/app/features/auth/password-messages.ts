import { MessagesByCode } from '../../core/http/problem-message';
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from '../../shared/forms/password.validator';

/** Texto de cada recusa de senha da API, o mesmo em toda tela que cria ou troca a senha. */
export const PASSWORD_POLICY_MESSAGES: MessagesByCode = {
  'identity.password.too_short': `A senha precisa ter pelo menos ${PASSWORD_MIN_LENGTH} caracteres`,
  'identity.password.too_long': `A senha pode ter no máximo ${PASSWORD_MAX_LENGTH} caracteres`,
  'identity.password.needs_letter_and_digit':
    'A senha precisa ter pelo menos uma letra e um número',
};
