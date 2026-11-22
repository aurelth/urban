import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { myAnnouncement, myAnnouncementPage } from '../../testing/announcements.fixtures';
import { AnnouncementsApi } from './announcements.api';
import { MarkAllResult, MyAnnouncementPage } from './announcements.models';

describe('AnnouncementsApi, mural do morador', () => {
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

  function params(request: { request: { params: { toString(): string } } }): string {
    return new URLSearchParams(request.request.params.toString()).toString();
  }

  it('lista o mural com GET e só manda a página quando não há filtro', () => {
    const page: MyAnnouncementPage = myAnnouncementPage([myAnnouncement()], { unread: 1 });
    let result: MyAnnouncementPage | undefined;

    api
      .listMine({ page: 1, pageSize: 10, unreadOnly: false, category: null })
      .subscribe((value) => (result = value));
    const request = http.expectOne((candidate) => candidate.url === '/api/announcements/mine');
    expect(request.request.method).toBe('GET');
    expect(params(request)).toBe('page=1&pageSize=10');
    request.flush(page);

    expect(result).toEqual(page);
  });

  it('manda unreadOnly=true e a categoria quando o morador filtra', () => {
    api.listMine({ page: 2, pageSize: 10, unreadOnly: true, category: 'maintenance' }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === '/api/announcements/mine');
    expect(params(request)).toBe('page=2&pageSize=10&unreadOnly=true&category=maintenance');
    request.flush(myAnnouncementPage([]));
  });

  it('marca um aviso como lido com POST, sem corpo', () => {
    let done = false;

    api.markRead('a-1').subscribe(() => (done = true));
    const request = http.expectOne('/api/announcements/a-1/read');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBe(true);
  });

  it('marca todos como lidos com POST e devolve quantos foram', () => {
    let result: MarkAllResult | undefined;

    api.markAllRead(null).subscribe((value) => (result = value));
    const request = http.expectOne((candidate) => candidate.url === '/api/announcements/read-all');
    expect(request.request.method).toBe('POST');
    expect(params(request)).toBe('');
    request.flush({ marked: 4 });

    expect(result).toEqual({ marked: 4 });
  });

  it('marca todos de uma categoria passando a categoria na consulta', () => {
    api.markAllRead('important').subscribe();

    const request = http.expectOne((candidate) => candidate.url === '/api/announcements/read-all');
    expect(params(request)).toBe('category=important');
    request.flush({ marked: 1 });
  });
});
