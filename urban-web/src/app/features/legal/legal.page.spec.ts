import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LEGAL_CONTENT, LEGAL_VERSION, LegalDocument } from './legal-content';
import { LegalPage } from './legal.page';

async function setup(document: LegalDocument): Promise<ComponentFixture<LegalPage>> {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(LegalPage);
  fixture.componentRef.setInput('document', document);
  await fixture.whenStable();
  return fixture;
}

describe('LegalPage', () => {
  it.each([
    ['terms', 'Termos de uso'],
    ['privacy', 'Política de privacidade'],
  ] as const)('mostra o documento %s com o titulo certo', async (document, title) => {
    const html: HTMLElement = (await setup(document)).nativeElement;

    expect(html.querySelector('h1')?.textContent?.trim()).toBe(title);
  });

  it.each(['terms', 'privacy'] as const)(
    'avisa que %s e um rascunho sem valor de aconselhamento juridico',
    async (document) => {
      const html: HTMLElement = (await setup(document)).nativeElement;

      const note = html.querySelector('[role="note"]')?.textContent ?? '';
      expect(note).toContain('Rascunho');
      expect(note).toContain('não é aconselhamento jurídico');
    },
  );

  it('mostra a versao do texto', async () => {
    const html: HTMLElement = (await setup('terms')).nativeElement;

    expect(html.textContent).toContain(`Versão ${LEGAL_VERSION}`);
  });

  it.each(['terms', 'privacy'] as const)('mostra todas as secoes de %s', async (document) => {
    const html: HTMLElement = (await setup(document)).nativeElement;
    const sections = LEGAL_CONTENT[document].sections;

    const headings = [...html.querySelectorAll('h2')].map((h2) => h2.textContent?.trim());
    expect(headings).toEqual(sections.map((section) => section.title));
    expect(html.querySelectorAll('section p').length).toBe(
      sections.reduce((total, section) => total + section.paragraphs.length, 0),
    );
  });

  it('oferece voltar ao Urban', async () => {
    const html: HTMLElement = (await setup('privacy')).nativeElement;

    expect(html.querySelector('a[href="/"]')?.textContent).toContain('Voltar ao Urban');
  });
});
