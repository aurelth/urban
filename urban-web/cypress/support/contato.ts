export const CONTACT_PATH = '/contato';
export const MESSAGES_PATH = '/admin/mensagens';
const SEND_URL = '/api/contact/messages';
const MESSAGES_LIST = /\/api\/admin\/contact-messages(\?.*)?$/;
const MESSAGE_READ = /\/api\/admin\/contact-messages\/[^/?]+\/read$/;
const MESSAGE_REPLY = /\/api\/admin\/contact-messages\/[^/?]+\/replies$/;

export interface StubReplyAttachment {
  fileName: string;
  contentType: string;
  size: number;
}

export interface StubReply {
  id: string;
  sentAt: string;
  body: string;
  attachments: StubReplyAttachment[];
}

export interface StubMessage {
  id: string;
  senderId: string;
  senderEmail: string;
  senderName: string | null;
  condominiumName: string | null;
  subject: string;
  body: string;
  sentAt: string;
  readAt: string | null;
  replies: StubReply[];
}

export const UNREAD_MESSAGE: StubMessage = {
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c6001',
  senderId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5001',
  senderEmail: 'ana@example.com',
  senderName: 'Ana',
  condominiumName: 'Residencial Aurora',
  subject: 'Corrigir o CNPJ do condomínio',
  body: 'Digitei o CNPJ errado no cadastro.\nPode corrigir para 11.222.333/0001-81?',
  sentAt: '2026-11-13T15:05:00+00:00',
  readAt: null,
  replies: [],
};

export const READ_MESSAGE: StubMessage = {
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c6002',
  senderId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5002',
  senderEmail: 'carlos@example.com',
  senderName: null,
  condominiumName: null,
  subject: 'Dúvida sobre o rateio',
  body: 'Como o rateio por fração ideal é calculado?',
  sentAt: '2026-11-12T13:05:00+00:00',
  readAt: '2026-11-12T20:00:00+00:00',
  replies: [],
};

/** O envio dá certo. */
export function stubContactSend(): void {
  cy.intercept('POST', SEND_URL, {
    statusCode: 201,
    body: { id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c6099', sentAt: '2026-11-13T15:05:00+00:00' },
  }).as('send');
}

/** O envio é recusado pela API (sem permissão, texto inválido, limite de requisições). */
export function stubContactSendRejected(statusCode: number, code: string): void {
  cy.intercept('POST', SEND_URL, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as('send');
}

/** Hora em que a API simulada diz que a resposta saiu (14:30 em Brasília). */
export const REPLY_SENT_AT = '2026-11-13T17:30:00+00:00';

export interface ReceivedReply {
  body: string;
  files: { fileName: string; contentType: string; size: number }[];
}

/**
 * Lê o corpo multipart que o navegador montou. O Cypress entrega o texto cru: cada parte começa depois da linha do
 * limite, com `Content-Disposition` (e `Content-Type`, nos arquivos), uma linha em branco e o conteúdo.
 */
export function readReply(request: {
  headers: Record<string, string | string[]>;
  body: unknown;
}): ReceivedReply {
  const type = String(request.headers['content-type']);
  const boundary = /boundary=(.+)$/.exec(type)?.[1] ?? '';
  const raw =
    typeof request.body === 'string'
      ? request.body
      : new TextDecoder().decode(request.body as ArrayBuffer);
  const reply: ReceivedReply = { body: '', files: [] };
  for (const part of raw.split(`--${boundary}`)) {
    const divider = part.indexOf('\r\n\r\n');
    if (divider < 0) {
      continue;
    }
    const head = part.slice(0, divider);
    const content = part.slice(divider + 4).replace(/\r\n$/, '');
    const name = /name="([^"]+)"/.exec(head)?.[1];
    const fileName = /filename="([^"]*)"/.exec(head)?.[1];
    if (name === 'body') {
      reply.body = content;
    } else if (name === 'attachments' && fileName !== undefined) {
      const contentType = /Content-Type: (.+)/i.exec(head)?.[1].trim() ?? '';
      reply.files.push({ fileName, contentType, size: content.length });
    }
  }
  return reply;
}

/** A API simulada guarda a caixa de entrada: filtra pelas não lidas, pagina, marca como lida e guarda as respostas. */
export function stubContactMessages(initial: StubMessage[] = [UNREAD_MESSAGE, READ_MESSAGE]): void {
  const messages = initial.map((message) => ({ ...message, replies: [...message.replies] }));
  cy.intercept('GET', MESSAGES_LIST, (request) => {
    const url = new URL(request.url);
    const unreadOnly = url.searchParams.get('status') === 'unread';
    const page = Number(url.searchParams.get('page') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 20);
    const matching = messages.filter((message) => !unreadOnly || message.readAt === null);
    request.reply({
      statusCode: 200,
      body: {
        items: matching.slice((page - 1) * pageSize, page * pageSize),
        total: matching.length,
        page,
        pageSize,
      },
    });
  }).as('messages');
  cy.intercept('POST', MESSAGE_READ, (request) => {
    const id = request.url.split('/').at(-2);
    const found = messages.find((message) => message.id === id);
    if (found !== undefined && found.readAt === null) {
      found.readAt = '2026-11-13T16:00:00+00:00';
    }
    request.reply({ statusCode: 204 });
  }).as('read');
  cy.intercept('POST', MESSAGE_REPLY, (request) => {
    const id = request.url.split('/').at(-2);
    const found = messages.find((message) => message.id === id);
    const received = readReply(request);
    const reply: StubReply = {
      id: `0198f3c1-6a4e-7c20-9d52-3f7a1b2c70${String(found?.replies.length ?? 0).padStart(2, '0')}`,
      sentAt: REPLY_SENT_AT,
      body: received.body,
      attachments: received.files,
    };
    if (found !== undefined) {
      found.replies.push(reply);
      found.readAt ??= REPLY_SENT_AT;
    }
    request.reply({
      statusCode: 201,
      body: { id: reply.id, sentAt: reply.sentAt, attachments: reply.attachments },
    });
  }).as('reply');
}

/** O envio da resposta é recusado pela API (o e-mail não saiu, anexo inválido, limite de requisições). */
export function stubReplyRejected(statusCode: number, code: string): void {
  cy.intercept('POST', MESSAGE_REPLY, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as('reply');
}
