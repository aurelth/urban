import { MessagesByCode } from '../../../core/http/problem-message';

/** Recusas da API ao aprovar ou recusar um pedido. 404 e 409 significam que a lista estava desatualizada. */
export const DECISION_MESSAGES: MessagesByCode = {
  'identity.registration_request.not_found': 'Este pedido não existe mais. A lista foi atualizada',
  'identity.user.invalid_transition': 'Este pedido já foi decidido. A lista foi atualizada',
  'identity.registration_request.forbidden': 'Só o administrador pode decidir pedidos de cadastro',
};
