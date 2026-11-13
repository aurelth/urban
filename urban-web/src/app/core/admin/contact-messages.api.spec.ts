import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  CONTACT_MESSAGES_URL,
  contactMessage,
  contactMessagesPage,
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
});
