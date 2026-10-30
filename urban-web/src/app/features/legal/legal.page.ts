import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLink } from '../../shared/ui/brand-link/brand.link';
import { LEGAL_CONTENT, LEGAL_VERSION, LegalDocument } from './legal-content';

/** Termos de uso e Política de privacidade: o documento vem do dado da rota (`data: { document }`). */
@Component({
  selector: 'urban-legal-page',
  imports: [RouterLink, BrandLink],
  templateUrl: './legal.page.html',
})
export class LegalPage {
  readonly document = input.required<LegalDocument>();

  protected readonly content = computed(() => LEGAL_CONTENT[this.document()]);
  protected readonly version = LEGAL_VERSION;
}
