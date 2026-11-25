import { signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { FileDownload } from '../../core/finance/pdf-download';
import { MessagesByCode } from '../../core/http/problem-message';
import { FileSaver } from './file-saver';

/** Esqueleto: marca a chave, mas ainda não salva o arquivo nem explica a falha. */
export class FileDownloads {
  readonly active = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  constructor(
    protected readonly files: FileSaver,
    protected readonly messages: MessagesByCode,
  ) {}

  async run(key: string, source: Observable<FileDownload>): Promise<boolean> {
    this.active.set(key);
    await firstValueFrom(source);
    this.active.set(null);
    return true;
  }

  clear(): void {
    this.active.set(null);
  }
}
