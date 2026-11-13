export const CONTACT_PATH = '/contato';
export const MESSAGES_PATH = '/admin/mensagens';
const SEND_URL = '/api/contact/messages';
const MESSAGES_LIST = /\/api\/admin\/contact-messages(\?.*)?$/;
const MESSAGE_READ = /\/api\/admin\/contact-messages\/[^/?]+\/read$/;

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

/** A API simulada guarda a caixa de entrada: filtra pelas não lidas, pagina e marca como lida. */
export function stubContactMessages(initial: StubMessage[] = [UNREAD_MESSAGE, READ_MESSAGE]): void {
  const messages = initial.map((message) => ({ ...message }));
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
}
