import { HttpErrorResponse } from '@angular/common/http';
import { toBatchIssues } from './batch-issues';

describe('toBatchIssues', () => {
  it('le a posicao e o codigo de cada problema do lote', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: {
        code: 'unit.identifier.already_exists',
        errors: [
          { position: 0, code: 'unit.identifier.already_exists' },
          { position: 7, code: 'unit.number.invalid' },
        ],
      },
    });

    expect(toBatchIssues(error)).toEqual([
      { position: 0, code: 'unit.identifier.already_exists' },
      { position: 7, code: 'unit.number.invalid' },
    ]);
  });

  it('descarta entradas mal formadas', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        errors: [
          { position: -1, code: 'a' },
          { position: 1.5, code: 'b' },
          { position: '2', code: 'c' },
          { position: 3 },
          null,
          'texto',
          { position: 4, code: 'unit.area.invalid' },
        ],
      },
    });

    expect(toBatchIssues(error)).toEqual([{ position: 4, code: 'unit.area.invalid' }]);
  });

  it.each([
    ['sem o campo errors', { code: 'x' }],
    ['com errors que nao e lista', { errors: 'x' }],
    ['com corpo vazio', null],
    ['com corpo de texto', 'falhou'],
  ])('devolve vazio %s', (_name, body) => {
    expect(toBatchIssues(new HttpErrorResponse({ status: 400, error: body }))).toEqual([]);
  });

  it('devolve vazio para algo que nao e uma resposta HTTP', () => {
    expect(toBatchIssues(new Error('falhou'))).toEqual([]);
  });
});
