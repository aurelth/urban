import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { problem } from '../../../testing/auth.fixtures';
import {
  accepted,
  setupComponent,
  settle,
  submitForm,
  typeEmail,
  typeField,
} from '../../../testing/component.harness';
import { RegisterPage } from './register.page';

const EMAIL = 'sindica@example.com';

function fill(html: HTMLElement, name = 'Marina', condominium = 'Residencial Aurora'): void {
  typeEmail(html, EMAIL);
  typeField(html, 'name', name);
  typeField(html, 'condominiumName', condominium);
}

describe('RegisterPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o titulo, os tres campos e o caminho para quem ja tem conta', async () => {
    const { fixture, html } = setupComponent(RegisterPage);
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Criar conta');
    expect(html.textContent).toContain('Cadastro de síndicos');
    expect(html.querySelector('input[type="email"]')).not.toBeNull();
    expect(html.textContent).toContain('Como prefere ser chamado');
    expect(html.textContent).toContain('Nome do condomínio');
    expect(html.querySelector('button[type="submit"]')?.textContent?.trim()).toBe('Pedir cadastro');
    expect(html.querySelector('a[href="/entrar"]')?.textContent?.trim()).toBe('Entrar');
  });

  it('pede o cadastro com o e-mail, o nome e o condominio, sem espacos nas pontas', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html, '  Marina  ', '  Residencial Aurora ');

    await submitForm(fixture);
    const request = http.expectOne('/api/auth/register');
    expect(request.request.body).toEqual({
      email: EMAIL,
      name: 'Marina',
      condominiumName: 'Residencial Aurora',
    });
    request.flush(null, accepted());
    await settle(fixture);
  });

  it('diz que o pedido foi recebido e que o link so chega se ele for aprovado', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html);

    await submitForm(fixture);
    http.expectOne('/api/auth/register').flush(null, accepted());
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Pedido recebido');
    const notice = html.querySelector('[role="status"]')?.textContent?.replace(/\s+/g, ' ');
    expect(notice).toContain(EMAIL);
    expect(notice).toContain('Se o pedido for aprovado');
    expect(notice).toContain('O link vale por 24 horas');
    expect(html.querySelector('form')).toBeNull();
  });

  it('volta ao formulario com os dados preenchidos para corrigir o pedido', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html);
    await submitForm(fixture);
    http.expectOne('/api/auth/register').flush(null, accepted());
    await settle(fixture);

    html.querySelector<HTMLButtonElement>('[data-testid="edit-request"]')!.click();
    await settle(fixture);

    expect(html.querySelector<HTMLInputElement>('input[type="email"]')?.value).toBe(EMAIL);
    expect(html.querySelector<HTMLInputElement>('input[formControlName="name"]')?.value).toBe(
      'Marina',
    );
    expect(
      html.querySelector<HTMLInputElement>('input[formControlName="condominiumName"]')?.value,
    ).toBe('Residencial Aurora');
  });

  it.each([
    ['sem o nome', 'Marina', '', 'Informe o nome do condomínio'],
    ['com o condominio de uma letra', 'Marina', 'A', 'O nome deve ter de 2 a 100 caracteres'],
    [
      'com o condominio grande demais',
      'Marina',
      'A'.repeat(101),
      'O nome deve ter de 2 a 100 caracteres',
    ],
    ['sem como prefere ser chamado', '', 'Residencial Aurora', 'Informe como prefere ser chamado'],
    ['com numero no nome', 'Marina 3', 'Residencial Aurora', 'Use de 2 a 40 letras, sem números'],
  ])('nao envia %s', async (_name, person, condominium, message) => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html, person, condominium);

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/register');
    expect(html.textContent).toContain(message);
  });

  it('nao envia com o e-mail invalido', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html);
    typeEmail(html, 'isto-nao-e-email');

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/register');
    expect(html.textContent).toContain('Informe um e-mail válido');
  });

  it.each([
    ['identity.display_name.invalid', 'Use de 2 a 40 letras, sem números'],
    [
      'identity.requested_condominium_name.invalid',
      'O nome do condomínio deve ter de 2 a 100 caracteres',
    ],
    ['identity.email.invalid', 'Informe um e-mail válido'],
  ])('mostra o motivo quando a API recusa com %s', async (code, message) => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html);

    await submitForm(fixture);
    http
      .expectOne('/api/auth/register')
      .flush(problem(400, code), { status: 400, statusText: 'Bad Request' });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain(message);
    expect(html.querySelector('form')).not.toBeNull();
  });

  it('pede para aguardar quando o limite de pedidos estoura', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html);

    await submitForm(fixture);
    http.expectOne('/api/auth/register').flush(null, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '30' },
    });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Aguarde 30 segundos');
  });

  it('trava o botao enquanto o pedido esta a caminho', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    fill(html);

    await submitForm(fixture);
    fixture.detectChanges();

    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Enviando');
    http.expectOne('/api/auth/register').flush(null, accepted());
  });
});
