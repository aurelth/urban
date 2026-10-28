import { Component, input } from '@angular/core';
import { MatAnchor } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/**
 * Aviso de link do e-mail que não serve mais (incompleto, já usado ou vencido), com o caminho
 * para pedir outro. Usado pelas telas que abrem com `?token=`: confirmar e-mail e redefinir senha.
 */
@Component({
  selector: 'urban-link-problem-notice',
  imports: [MatAnchor, MatIcon, RouterLink],
  templateUrl: './link-problem.notice.html',
  host: { class: 'flex flex-col items-center gap-3 text-center' },
})
export class LinkProblemNotice {
  readonly heading = input.required<string>();
  readonly message = input.required<string>();
  readonly actionLabel = input.required<string>();
  readonly actionLink = input.required<string>();
}
