import { Component, input } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';

/**
 * Fim dos formulários das telas de acesso: o aviso de erro da API e o botão de envio, que fica
 * desabilitado e troca de texto enquanto a chamada está em andamento. Vai dentro do
 * `<form (ngSubmit)>` da tela, que continua dona do envio.
 */
@Component({
  selector: 'urban-form-submit',
  imports: [MatButton, MatIcon],
  templateUrl: './form-submit.html',
  host: { class: 'flex flex-col' },
})
export class FormSubmit {
  readonly label = input.required<string>();
  readonly busyLabel = input.required<string>();
  readonly busy = input(false);
  /** Para telas em que não há o que enviar (nada mudou): o botão fica desabilitado, mas sem trocar o texto. */
  readonly disabled = input(false);
  readonly error = input<string | null>(null);
}
