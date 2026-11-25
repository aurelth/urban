import { HttpErrorResponse } from '@angular/common/http';
import { jest } from '@jest/globals';
import { Subject } from 'rxjs';
import { FileDownload } from '../../core/finance/pdf-download';
import { FileDownloads } from './file-downloads';
import { FileSaver } from './file-saver';

const MESSAGES = { 'finance.payment.not_found': 'Este recibo não está disponível' };
const PDF: FileDownload = { blob: new Blob(['%PDF-1.7']), fileName: 'recibo-000001.pdf' };

function problem(status: number, code: string | null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: code === null ? null : { status, code } });
}

describe('FileDownloads', () => {
  let save: jest.Mock<(blob: Blob, fileName: string) => void>;
  let downloads: FileDownloads;

  beforeEach(() => {
    save = jest.fn<(blob: Blob, fileName: string) => void>();
    downloads = new FileDownloads({ save } as unknown as FileSaver, MESSAGES);
  });

  it('comeca sem baixar nada e sem erro', () => {
    expect(downloads.active()).toBeNull();
    expect(downloads.error()).toBeNull();
  });

  it('marca a chave que esta baixando enquanto o arquivo nao chega', async () => {
    const source = new Subject<FileDownload>();

    const done = downloads.run('p1', source);
    expect(downloads.active()).toBe('p1');
    source.next(PDF);
    await done;

    expect(downloads.active()).toBeNull();
  });

  it('salva o arquivo com o nome que a API sugeriu e responde verdadeiro', async () => {
    const source = new Subject<FileDownload>();

    const done = downloads.run('p1', source);
    source.next(PDF);

    await expect(done).resolves.toBe(true);
    expect(save).toHaveBeenCalledTimes(1);
    expect(save).toHaveBeenCalledWith(PDF.blob, 'recibo-000001.pdf');
    expect(downloads.error()).toBeNull();
  });

  it('explica a falha com o texto do codigo, nao salva nada e responde falso', async () => {
    const source = new Subject<FileDownload>();

    const done = downloads.run('p1', source);
    source.error(problem(404, 'finance.payment.not_found'));

    await expect(done).resolves.toBe(false);
    expect(save).not.toHaveBeenCalled();
    expect(downloads.error()).toBe('Este recibo não está disponível');
    expect(downloads.active()).toBeNull();
  });

  it('usa o texto padrao quando o codigo nao tem texto proprio', async () => {
    const source = new Subject<FileDownload>();

    const done = downloads.run('p1', source);
    source.error(problem(429, null));

    await expect(done).resolves.toBe(false);
    expect(downloads.error()).toContain('Muitas tentativas');
  });

  it('a proxima tentativa apaga o erro da anterior', async () => {
    const failing = new Subject<FileDownload>();
    const failed = downloads.run('p1', failing);
    failing.error(problem(500, null));
    await failed;
    expect(downloads.error()).not.toBeNull();

    const next = new Subject<FileDownload>();
    const done = downloads.run('p2', next);
    expect(downloads.error()).toBeNull();
    next.next(PDF);

    await expect(done).resolves.toBe(true);
    expect(downloads.error()).toBeNull();
  });

  it('clear apaga o erro', async () => {
    const source = new Subject<FileDownload>();
    const done = downloads.run('p1', source);
    source.error(problem(500, null));
    await done;

    downloads.clear();

    expect(downloads.error()).toBeNull();
  });
});
