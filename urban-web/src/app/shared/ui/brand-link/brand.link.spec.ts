import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { signInAs } from '../../../testing/auth.fixtures';
import { BrandLink } from './brand.link';

@Component({ template: '' })
class Blank {}

async function setup(
  signedIn: boolean,
  compact = false,
): Promise<{
  fixture: ComponentFixture<BrandLink>;
  link: HTMLAnchorElement;
  router: Router;
}> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: '', pathMatch: 'full', component: Blank },
        { path: 'conta', component: Blank },
        { path: 'entrar', component: Blank },
        { path: 'esqueci-a-senha', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  if (signedIn) {
    await signInAs(TestBed.inject(SessionStore), TestBed.inject(HttpTestingController));
  }
  const fixture = TestBed.createComponent(BrandLink);
  fixture.componentRef.setInput('compact', compact);
  await fixture.whenStable();
  return {
    fixture,
    link: fixture.nativeElement.querySelector('a'),
    router: TestBed.inject(Router),
  };
}

describe('BrandLink', () => {
  afterEach(() => TestBed.inject(HttpTestingController).match(() => true));

  it('mostra o nome da marca como link', async () => {
    const { link } = await setup(true);

    expect(link.textContent?.trim()).toBe('Urban');
  });

  it('diz no nome acessível para onde o link leva, sem perder a palavra visível', async () => {
    const { link } = await setup(true);

    expect(link.getAttribute('aria-label')).toBe('Urban, ir para a página inicial');
  });

  it('põe o U colorido do favicon antes do nome, só como enfeite para leitores de tela', async () => {
    const { link } = await setup(true);

    const mark = link.querySelector('svg');

    expect(mark?.getAttribute('data-testid')).toBe('brand-mark');
    expect(mark?.getAttribute('aria-hidden')).toBe('true');
    expect(mark?.getAttribute('viewBox')).toBe('0 0 64 64');
    expect(mark?.querySelector('rect')?.getAttribute('class')).toContain('fill-accent-fill');
    expect(mark?.querySelector('path')?.getAttribute('class')).toContain('stroke-on-accent-fill');
    expect(link.firstElementChild).toBe(mark);
  });

  it('no modo compacto mostra só o U, e o nome continua no nome acessível', async () => {
    const { link } = await setup(true, true);

    expect(link.querySelector('svg')).not.toBeNull();
    expect(link.textContent?.trim()).toBe('');
    expect(link.getAttribute('aria-label')).toBe('Urban, ir para a página inicial');
  });

  it('para quem está logado, a página inicial é a raiz', async () => {
    const { link } = await setup(true);

    expect(link.getAttribute('href')).toBe('/');
  });

  it('para quem não entrou, a página inicial é a entrada', async () => {
    const { link } = await setup(false);

    expect(link.getAttribute('href')).toBe('/entrar');
  });

  it('volta para a raiz a partir de qualquer tela da área logada', async () => {
    const { fixture, link, router } = await setup(true);
    await router.navigateByUrl('/conta');

    link.click();
    await fixture.whenStable();

    expect(router.url).toBe('/');
  });

  it('leva quem não entrou direto à entrada, sem guardar destino', async () => {
    const { fixture, link, router } = await setup(false);
    await router.navigateByUrl('/esqueci-a-senha');

    link.click();
    await fixture.whenStable();

    expect(router.url).toBe('/entrar');
  });
});
