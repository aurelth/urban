import { signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { FileDownload } from '../../core/finance/pdf-download';
import { toApiProblem } from '../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../core/http/problem-message';
import { FileSaver } from './file-saver';

/**
 * O estado de uma tela que baixa arquivos (faturas, recibos): qual está baixando (`active`, pela chave de quem chamou, para
 * travar só o botão certo) e o texto da última falha. Quem usa dá os textos dos códigos da API da própria tela.
 */
export class FileDownloads {
  readonly active = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  constructor(
    private readonly files: FileSaver,
    private readonly messages: MessagesByCode,
  ) {}

  /** Baixa e salva o arquivo; responde se deu certo. A falha vira o texto em `error`, nunca uma exceção. */
  async run(key: string, source: Observable<FileDownload>): Promise<boolean> {
    this.error.set(null);
    this.active.set(key);
    try {
      const download = await firstValueFrom(source);
      this.files.save(download.blob, download.fileName);
      return true;
    } catch (failure) {
      this.error.set(describeProblem(toApiProblem(failure), this.messages));
      return false;
    } finally {
      this.active.set(null);
    }
  }

  clear(): void {
    this.error.set(null);
  }
}
