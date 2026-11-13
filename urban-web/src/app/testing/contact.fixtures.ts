import {
  ContactMessage,
  ContactMessagesPage,
  ContactReply,
  ContactReplyReceipt,
} from '../core/admin/contact-messages.models';

export const CONTACT_MESSAGES_URL = '/api/admin/contact-messages';
export const SEND_CONTACT_URL = '/api/contact/messages';

export function contactMessage(overrides: Partial<ContactMessage> = {}): ContactMessage {
  return {
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
    ...overrides,
  };
}

export const READ_MESSAGE: ContactMessage = contactMessage({
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c6002',
  senderId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5002',
  senderEmail: 'carlos@example.com',
  senderName: null,
  condominiumName: null,
  subject: 'Dúvida sobre o rateio',
  body: 'Como o rateio por fração ideal é calculado?',
  sentAt: '2026-11-12T13:05:00+00:00',
  readAt: '2026-11-12T20:00:00+00:00',
});

export function contactMessagesPage(
  items: ContactMessage[],
  total = items.length,
  page = 1,
  pageSize = 20,
): ContactMessagesPage {
  return { items, total, page, pageSize };
}

export function contactReply(overrides: Partial<ContactReply> = {}): ContactReply {
  return {
    id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c7001',
    sentAt: '2026-11-13T17:30:00+00:00',
    body: 'Corrigi o CNPJ no cadastro.\nConfira em Meus dados.',
    attachments: [],
    ...overrides,
  };
}

export const REPLY_RECEIPT: ContactReplyReceipt = {
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c7001',
  sentAt: '2026-11-13T17:30:00+00:00',
  attachments: [],
};

/** Rota da resposta a uma mensagem. */
export function replyUrl(messageId: string): string {
  return `${CONTACT_MESSAGES_URL}/${messageId}/replies`;
}

/** Arquivo de teste com o tamanho dito, sem alocar os bytes (o tamanho é o que a tela confere). */
export function fileOfSize(name: string, size: number, type = 'application/pdf'): File {
  const file = new File(['x'], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}
