import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { problem } from '../../../testing/auth.fixtures';
import {
  Harness,
  accepted,
  setupComponent,
  settle,
  submitForm,
  typeEmail,
} from '../../../testing/component.harness';
import { LinkRequestCard } from './link-request.card';

@Component({
  imports: [LinkRequestCard],
  template: `
    <urban-link-request-card heading="Título de teste" intro="Introdução de teste" [send]="send">
      <ng-template #notice let-email
        ><p>Aviso para {{ email }}</p></ng-template
      >
      <a sentAction href="/volta">Voltar</a>
      Rodapé de teste
    </urban-link-request-card>
  `,
})
class HostComponent {
  private readonly http = inject(HttpClient);

  protected readonly send = (email: string): Observable<void> =>
    this.http.post<void>('/api/teste', { email });
}

function setup(): Harness<HostComponent> {
  return setupComponent(HostComponent);
}

describe('LinkRequestCard', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o título, a introdução, o campo de e-mail e o rodapé da tela', async () => {
    const { fixture, html } = setup();
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Título de teste');
    expect(html.textContent).toContain('Introdução de teste');
    expect(html.querySelector('input[type="email"]')?.getAttribute('autocomplete')).toBe('email');
    expect(html.querySelector('p:last-of-type')?.textContent).toContain('Rodapé de teste');
    expect(html.querySelector('a[href="/volta"]')).toBeNull();
  });

  it('nao chama a API e foca o campo quando o e-mail esta vazio', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/teste');
    expect(html.textContent).toContain('Informe o e-mail');
    expect(document.activeElement).toBe(html.querySelector('input[type="email"]'));
  });

  it('pede um e-mail valido', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'isto-nao-e-email');

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/teste');
    expect(html.textContent).toContain('Informe um e-mail válido');
  });

  it('envia so o e-mail e mostra o aviso da tela com a acao extra', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submitForm(fixture);
    const request = http.expectOne('/api/teste');
    expect(request.request.body).toEqual({ email: 'sindica@example.com' });
    request.flush(null, accepted());
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Verifique seu e-mail');
    expect(html.querySelector('h1')?.classList).toContain('text-success');
    expect(html.querySelector('[role="status"] mat-icon')?.parentElement?.classList).toContain(
      'text-success',
    );
    const notice = html.querySelector('[role="status"]')?.textContent;
    expect(notice).toContain('Aviso para sindica@example.com');
    expect(notice).toContain('caixa de spam');
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/volta"]')?.textContent).toBe('Voltar');
  });

  it('volta ao formulario, com o e-mail preenchido, ao escolher outro e-mail', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');
    await submitForm(fixture);
    http.expectOne('/api/teste').flush(null, accepted());
    await settle(fixture);

    html.querySelector<HTMLButtonElement>('button[type="button"]')!.click();
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Título de teste');
    expect(html.querySelector<HTMLInputElement>('input[type="email"]')?.value).toBe(
      'sindica@example.com',
    );
  });

  it('desabilita o botao enquanto envia e libera depois da resposta', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');
    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Enviar link');

    await submitForm(fixture);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Enviando');

    http.expectOne('/api/teste').flush(null, { status: 503, statusText: 'Service Unavailable' });
    await settle(fixture);

    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Enviar link');
  });

  it('mostra o motivo quando a API recusa o e-mail', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submitForm(fixture);
    http.expectOne('/api/teste').flush(problem(400, 'identity.email.invalid'), {
      status: 400,
      statusText: 'Bad Request',
    });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Informe um e-mail válido');
    expect(html.querySelector('form')).not.toBeNull();
  });

  it('pede para aguardar quando o limite de pedidos estoura', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submitForm(fixture);
    http.expectOne('/api/teste').flush(null, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '30' },
    });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Aguarde 30 segundos');
  });

  it('limpa o aviso de erro quando a pessoa tenta de novo', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');
    await submitForm(fixture);
    http.expectOne('/api/teste').flush(null, { status: 503, statusText: 'Service Unavailable' });
    await settle(fixture);
    expect(html.querySelector('[role="alert"]')).not.toBeNull();

    await submitForm(fixture);
    fixture.detectChanges();

    expect(html.querySelector('[role="alert"]')).toBeNull();
    http.expectOne('/api/teste');
  });
});
