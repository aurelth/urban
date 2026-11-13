import { MessagesByCode } from '../../../core/http/problem-message';

/** Recusas da API ao marcar uma mensagem como lida. O 404 significa que a lista estava desatualizada. */
export const MARK_READ_MESSAGES: MessagesByCode = {
  'identity.contact.message_not_found': 'Esta mensagem não existe mais. A lista foi atualizada',
  'identity.contact.read_forbidden': 'Só o administrador pode ler as mensagens',
};
