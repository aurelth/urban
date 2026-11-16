import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** O navegador guarda o endereço temporário até ele ser liberado; um segundo dá folga para o download começar. */
const RELEASE_DELAY_MS = 1000;

/** Entrega um arquivo que já está na memória à pessoa, como um download comum. */
@Injectable({ providedIn: 'root' })
export class FileSaver {
  private readonly document = inject(DOCUMENT);

  save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = this.document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.hidden = true;
    this.document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), RELEASE_DELAY_MS);
  }
}
