import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  CONTACT_MESSAGES_URL,
  REPLY_RECEIPT,
  contactMessage,
  contactMessagesPage,
  fileOfSize,
  replyUrl,
} from '../../testing/contact.fixtures';
import { ContactMessagesApi } from './contact-messages.api';
import { DEFAULT_CONTACT_MESSAGES_QUERY } from './contact-messages.models';

function setup(): { api: ContactMessagesApi; http: HttpTestingController } {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(ContactMessagesApi), http: TestBed.inject(HttpTestingController) };
}

const NO_CONTENT = { status: 204, statusText: 'No Content' };

describe('ContactMessagesApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  describe('list', () => {
    it('sem filtro, manda só a página e o tamanho', () => {
      const { api, http } = setup();
      const received: unknown[] = [];

      api.list(DEFAULT_CONTACT_MESSAGES_QUERY).subscribe((page) => received.push(page));

      const request = http.expectOne((candidate) => candidate.url === CONTACT_MESSAGES_URL);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.toString()).toBe('page=1&pageSize=20');
      request.flush(contactMessagesPage([contactMessage()]));
      expect(received).toEqual([contactMessagesPage([contactMessage()])]);
    });

    it('manda a situação preenchida', () => {
      const { api, http } = setup();

      api.list({ status: 'unread', page: 2, pageSize: 20 }).subscribe();

      const request = http.expectOne((candidate) => candidate.url === CONTACT_MESSAGES_URL);
      expect(request.request.params.toString()).toBe('status=unread&page=2&pageSize=20');
      request.flush(contactMessagesPage([]));
    });
  });

  it('marca uma mensagem como lida pelo id', () => {
    const { api, http } = setup();

    api.markRead('abc-1').subscribe();

    const request = http.expectOne(`${CONTACT_MESSAGES_URL}/abc-1/read`);
    expect(request.request.method).toBe('POST');
    request.flush(null, NO_CONTENT);
  });

  describe('reply', () => {
    it('manda o texto aparado e os arquivos num formulário multipart, e devolve o recibo', () => {
      const { api, http } = setup();
      const received: unknown[] = [];
      const pdf = fileOfSize('orcamento.pdf', 100);
      const png = fileOfSize('foto.png', 200, 'image/png');

      api.reply('abc-1', '  Corrigi o cadastro.  ', [pdf, png]).subscribe((r) => received.push(r));

      const request = http.expectOne(replyUrl('abc-1'));
      expect(request.request.method).toBe('POST');
      const form = request.request.body as FormData;
      expect(form).toBeInstanceOf(FormData);
      expect(form.get('body')).toBe('Corrigi o cadastro.');
      expect(form.getAll('attachments').map((file) => (file as File).name)).toEqual([
        'orcamento.pdf',
        'foto.png',
      ]);
      request.flush(REPLY_RECEIPT);
      expect(received).toEqual([REPLY_RECEIPT]);
    });

    it('não define o Content-Type: o navegador precisa pôr o limite do multipart', () => {
      const { api, http } = setup();

      api.reply('abc-1', 'Corrigi o cadastro.', []).subscribe();

      const request = http.expectOne(replyUrl('abc-1'));
      expect(request.request.headers.has('Content-Type')).toBe(false);
      request.flush(REPLY_RECEIPT);
    });

    it('sem arquivos, manda só o texto', () => {
      const { api, http } = setup();

      api.reply('abc-1', 'Corrigi o cadastro.', []).subscribe();

      const request = http.expectOne(replyUrl('abc-1'));
      const form = request.request.body as FormData;
      expect(form.get('body')).toBe('Corrigi o cadastro.');
      expect(form.getAll('attachments')).toEqual([]);
      request.flush(REPLY_RECEIPT);
    });
  });
});
