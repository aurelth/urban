import { MessagesByCode } from '../../../core/http/problem-message';

/** Recusas da API ao suspender ou reativar um síndico. 404 e 409 significam que a lista estava desatualizada. */
export const MANAGER_MESSAGES: MessagesByCode = {
  'identity.administration.forbidden': 'Só o administrador pode administrar os síndicos',
  'identity.manager.not_found': 'Este síndico não existe mais. A lista foi atualizada',
  'identity.user.invalid_transition': 'A situação deste síndico mudou. A lista foi atualizada',
  'identity.user.reason_required': 'Informe o motivo da suspensão',
  'identity.user.reason_too_long': 'O motivo pode ter até 500 caracteres',
};
