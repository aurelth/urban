import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';

/**
 * A marca "Urban" como link para a página inicial, igual em todas as telas (barra da área logada, moldura da
 * entrada e páginas públicas). Fonte, tamanho e cor vêm de quem a usa. Quem está logado vai para a raiz, que
 * leva ao painel (ou ao cadastro do condomínio, se ainda não fez); quem não está vai direto para a entrada, sem
 * passar por um redirecionamento que guardaria o painel como destino do login.
 */
@Component({
  selector: 'urban-brand-link',
  imports: [RouterLink],
  templateUrl: './brand.link.html',
})
export class BrandLink {
  private readonly session = inject(SessionStore);

  protected readonly home = computed(() =>
    this.session.status() === 'authenticated' ? '/' : '/entrar',
  );
}
