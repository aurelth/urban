import { MessagesByCode } from '../../core/http/problem-message';

export const SUBJECT_LIMITS = { min: 3, max: 120 } as const;
export const BODY_LIMITS = { min: 10, max: 2000 } as const;

/** Texto de cada erro de validação do assunto, na ordem em que valem. */
export const SUBJECT_ERRORS: Readonly<Record<string, string>> = {
  required: 'Informe o assunto',
  tooShort: `O assunto precisa ter ao menos ${SUBJECT_LIMITS.min} caracteres`,
  tooLong: `O assunto pode ter até ${SUBJECT_LIMITS.max} caracteres`,
};

/** Recusas da API ao enviar a mensagem. */
export const SEND_MESSAGES: MessagesByCode = {
  'identity.contact.subject_invalid': `O assunto precisa ter de ${SUBJECT_LIMITS.min} a ${SUBJECT_LIMITS.max} caracteres, em uma só linha`,
  'identity.contact.body_invalid': `A mensagem precisa ter de ${BODY_LIMITS.min} a ${BODY_LIMITS.max} caracteres`,
  'identity.contact.send_forbidden': 'Só o síndico pode enviar mensagens ao dono da plataforma',
};
