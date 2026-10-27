import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { problem } from '../../testing/auth.fixtures';
import { provideSessionRestore } from './session.providers';
import { SessionStore } from './session.store';

describe('provideSessionRestore', () => {
  it('comeca a restaurar a sessao quando o app inicia, sem travar a inicializacao', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideSessionRestore()],
    });

    await TestBed.inject(ApplicationInitStatus).donePromise;
    const http = TestBed.inject(HttpTestingController);
    const refresh = http.expectOne('/api/auth/refresh');
    expect(TestBed.inject(SessionStore).status()).toBe('unknown');

    refresh.flush(problem(401, 'identity.refresh.invalid_session'), {
      status: 401,
      statusText: 'Unauthorized',
    });
    await TestBed.inject(SessionStore).ready();

    expect(TestBed.inject(SessionStore).status()).toBe('anonymous');
    http.verify();
  });
});
