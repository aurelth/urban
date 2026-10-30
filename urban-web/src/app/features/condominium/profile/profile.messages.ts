import { MessagesByCode } from '../../../core/http/problem-message';
import { REGISTER_CONDOMINIUM_MESSAGES } from '../register/register-condominium.messages';

/** Texto da tela "Meus dados" para cada código que a edição pode receber da API. As recusas de campo são as do cadastro. */
export const PROFILE_MESSAGES: MessagesByCode = {
  ...REGISTER_CONDOMINIUM_MESSAGES,
  'condominium.cpf.already_registered': 'Este CPF já está cadastrado neste condomínio',
  'condominium.version.required': 'Recarregue os dados antes de salvar',
  'identity.password.incorrect': 'Senha incorreta',
  'identity.login.locked_out':
    'Conta bloqueada por muitas tentativas. Tente novamente em 15 minutos',
  'identity.user.account_suspended': 'Acesso suspenso. Fale com o suporte',
  'identity.user.account_deactivated': 'Conta desativada. Fale com o síndico',
};
