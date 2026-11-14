import { MessagesByCode } from '../../core/http/problem-message';

/** Texto da tela para cada recusa que o convite do morador pode receber da API. */
export const ACCESS_MESSAGES: MessagesByCode = {
  'condominium.resident.email_required': 'Cadastre o e-mail do morador para poder convidá-lo',
  'condominium.resident.not_found': 'Este morador não existe mais. Recarregue a página',
  'identity.resident_access.email_unavailable':
    'Este e-mail já tem uma conta no Urban e não pode receber outro convite. Corrija o e-mail do morador',
  'identity.resident_access.not_pending':
    'Este morador já criou a senha. Recarregue a página para ver a situação atual',
  'identity.resident_access.resend_too_soon':
    'Um convite acabou de ser enviado. Espere um minuto para enviar outro',
  'identity.resident_access.not_found':
    'Este morador não tem convite para reenviar. Recarregue a página',
  'identity.resident_access.forbidden': 'Só o síndico com a conta ativa pode convidar moradores',
};
