import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { announcement, announcementPage } from '../../testing/announcements.fixtures';
import { AnnouncementsApi } from './announcements.api';
import { Announcement, AnnouncementPage, AnnouncementPayload } from './announcements.models';

const PAYLOAD: AnnouncementPayload = {
  title: 'Água cortada na terça',
  body: 'A rua será interditada.',
  category: 'maintenance',
  scope: 'block',
  block: 'A',
  unitId: null,
};

describe('AnnouncementsApi', () => {
  let api: AnnouncementsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(AnnouncementsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lista a página pedida com GET e devolve o que a API respondeu', () => {
    const page: AnnouncementPage = announcementPage([announcement()], 21, 2);
    let result: AnnouncementPage | undefined;

    api.list(2, 20).subscribe((value) => (result = value));
    const request = http.expectOne((candidate) => candidate.url === '/api/announcements');
    expect(request.request.method).toBe('GET');
    expect(new URLSearchParams(request.request.params.toString()).toString()).toBe(
      'page=2&pageSize=20',
    );
    request.flush(page);

    expect(result).toEqual(page);
  });

  it('publica com POST, levando o comunicado como está', () => {
    const created = announcement();
    let result: Announcement | undefined;

    api.create(PAYLOAD).subscribe((value) => (result = value));
    const request = http.expectOne('/api/announcements');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(PAYLOAD);
    request.flush(created);

    expect(result).toEqual(created);
  });

  it('altera com PUT, levando a versão lida', () => {
    const changed = announcement();

    api.update(changed.id, { ...PAYLOAD, version: '41' }).subscribe();
    const request = http.expectOne(`/api/announcements/${changed.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ...PAYLOAD, version: '41' });
    request.flush(changed);
  });

  it('exclui com DELETE, levando a versão na consulta', () => {
    let done = false;

    api.remove('a-1', '7').subscribe(() => (done = true));
    const request = http.expectOne(
      (candidate) => candidate.url === '/api/announcements/a-1' && candidate.method === 'DELETE',
    );
    expect(request.request.params.get('version')).toBe('7');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBe(true);
  });
});
