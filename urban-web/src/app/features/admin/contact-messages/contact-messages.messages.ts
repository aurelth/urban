import { MessagesByCode } from '../../../core/http/problem-message';
import { ATTACHMENT_LIMITS, REPLY_BODY_LIMITS, formatFileSize } from './reply-attachments';

/** Recusas da API ao marcar uma mensagem como lida. O 404 significa que a lista estava desatualizada. */
export const MARK_READ_MESSAGES: MessagesByCode = {
  'identity.contact.message_not_found': 'Esta mensagem não existe mais. A lista foi atualizada',
  'identity.contact.read_forbidden': 'Só o administrador pode ler as mensagens',
};

/**
 * Recusas da API ao responder. Em todas, menos no 404, nada foi enviado nem registrado. O 413 também pode vir do nginx, sem
 * código (o texto genérico vale); a tela já confere tamanho e quantidade antes de enviar.
 */
export const REPLY_MESSAGES: MessagesByCode = {
  'identity.contact.reply_body_invalid': `A resposta precisa ter de ${REPLY_BODY_LIMITS.min} a ${REPLY_BODY_LIMITS.max} caracteres`,
  'identity.contact.attachment_name_invalid':
    'O nome de um dos arquivos não pode ser usado. Renomeie o arquivo e tente de novo',
  'identity.contact.attachment_type_not_allowed':
    'Só PDF, PNG, JPG, DOCX, XLSX, CSV e TXT podem ser anexados',
  'identity.contact.attachment_content_invalid':
    'O conteúdo de um dos arquivos não confere com o tipo dele. Escolha outro arquivo',
  'identity.contact.attachment_too_large': `Cada arquivo pode ter até ${formatFileSize(ATTACHMENT_LIMITS.maxFileBytes)}`,
  'identity.contact.attachments_too_many': `Anexe no máximo ${ATTACHMENT_LIMITS.maxFiles} arquivos`,
  'identity.contact.attachments_total_too_large': `Os anexos somam mais de ${formatFileSize(ATTACHMENT_LIMITS.maxTotalBytes)}`,
  'identity.contact.reply_forbidden': 'Só o administrador pode responder às mensagens',
  'identity.contact.reply_recipient_unavailable':
    'A conta de quem escreveu não existe mais. Não há para onde enviar a resposta',
  'identity.contact.reply_not_delivered':
    'O e-mail não pôde ser entregue ao provedor de envio. Nada foi registrado; tente de novo em instantes',
  'identity.contact.message_not_found': 'Esta mensagem não existe mais. A lista foi atualizada',
  'request.invalid': 'O envio não pôde ser lido. Confira os arquivos e tente de novo',
};

/** Erros de validação do texto da resposta, na ordem em que valem. */
export const REPLY_BODY_ERRORS: Readonly<Record<string, string>> = {
  required: 'Informe a resposta',
  tooShort: `A resposta precisa ter ao menos ${REPLY_BODY_LIMITS.min} caracteres`,
  tooLong: `A resposta pode ter até ${REPLY_BODY_LIMITS.max} caracteres`,
};
