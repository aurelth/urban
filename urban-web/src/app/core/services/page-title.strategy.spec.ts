import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { APP_TITLE, composeTitle, providePageTitle } from './page-title.strategy';

@Component({ template: '' })
class BlankPage {}

describe('composeTitle', () => {
  it('põe o nome da aplicação primeiro e o da tela depois', () => {
    expect(composeTitle('Entrar')).toBe('Urban - Gestão condominial | Entrar');
  });

  it('usa só o nome da aplicação quando a tela não tem título', () => {
    expect(composeTitle(undefined)).toBe(APP_TITLE);
  });
});

describe('providePageTitle', () => {
  it('mostra o nome da aplicação na aba em todas as telas, com ou sem título próprio', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'entrar', title: 'Entrar', component: BlankPage },
          { path: 'sem-titulo', component: BlankPage },
        ]),
        providePageTitle(),
      ],
    });
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/entrar');
    expect(document.title).toBe('Urban - Gestão condominial | Entrar');

    await router.navigateByUrl('/sem-titulo');
    expect(document.title).toBe('Urban - Gestão condominial');
  });
});
