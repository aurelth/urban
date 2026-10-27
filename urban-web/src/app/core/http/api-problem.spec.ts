import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { toApiProblem } from './api-problem';

describe('toApiProblem', () => {
  it('extrai status, code e detail de um problem+json', () => {
    const error = new HttpErrorResponse({
      status: 401,
      error: { code: 'identity.login.invalid_credentials', detail: 'E-mail ou senha incorretos' },
    });

    expect(toApiProblem(error)).toEqual({
      status: 401,
      code: 'identity.login.invalid_credentials',
      detail: 'E-mail ou senha incorretos',
      retryAfterSeconds: null,
    });
  });

  it('le o Retry-After de um 429', () => {
    const error = new HttpErrorResponse({
      status: 429,
      headers: new HttpHeaders({ 'Retry-After': '42' }),
    });

    expect(toApiProblem(error).retryAfterSeconds).toBe(42);
  });

  it.each(['0', '-3', 'abc', '', '1.5'])('ignora o Retry-After invalido "%s"', (value) => {
    const error = new HttpErrorResponse({
      status: 429,
      headers: new HttpHeaders({ 'Retry-After': value }),
    });

    expect(toApiProblem(error).retryAfterSeconds).toBeNull();
  });

  it('trata a falta de resposta (rede fora) como status 0', () => {
    const error = new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') });

    expect(toApiProblem(error)).toEqual({
      status: 0,
      code: null,
      detail: null,
      retryAfterSeconds: null,
    });
  });

  it.each(['<html>erro</html>', null, 42])('ignora um corpo que nao e objeto (%p)', (body) => {
    const error = new HttpErrorResponse({ status: 500, error: body });

    expect(toApiProblem(error)).toEqual({
      status: 500,
      code: null,
      detail: null,
      retryAfterSeconds: null,
    });
  });

  it('ignora code e detail que nao sao texto', () => {
    const error = new HttpErrorResponse({ status: 400, error: { code: 5, detail: { x: 1 } } });

    expect(toApiProblem(error)).toEqual({
      status: 400,
      code: null,
      detail: null,
      retryAfterSeconds: null,
    });
  });

  it('aceita qualquer outra coisa lancada como falta de resposta', () => {
    expect(toApiProblem(new Error('falhou'))).toEqual({
      status: 0,
      code: null,
      detail: null,
      retryAfterSeconds: null,
    });
  });
});
